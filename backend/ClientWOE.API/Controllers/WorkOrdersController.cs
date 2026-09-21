using System.Globalization;
using ClientWOE.API.Data;
using ClientWOE.API.DTOs;
using ClientWOE.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ClientWOE.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkOrdersController : ControllerBase
{
    private const int MaxListRows = 200;
    private const int MaxSaveAttempts = 5;

    private readonly WoeDbContext _db;
    private readonly ILogger<WorkOrdersController> _logger;

    public WorkOrdersController(WoeDbContext db, ILogger<WorkOrdersController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// List work orders (newest first), optionally filtered by client and order date range.
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<WorkOrderSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<WorkOrderSummaryDto>>> GetAll(
        [FromQuery] int? clientId,
        [FromQuery(Name = "from")] DateTime? fromDate,
        [FromQuery(Name = "to")] DateTime? toDate)
    {
        var query = _db.WorkOrders.AsNoTracking().AsQueryable();

        if (clientId.HasValue)
        {
            query = query.Where(w => w.ClientId == clientId.Value);
        }

        if (fromDate.HasValue)
        {
            var start = fromDate.Value.Date;
            query = query.Where(w => w.OrderDate >= start);
        }

        if (toDate.HasValue)
        {
            var endExclusive = toDate.Value.Date.AddDays(1);
            query = query.Where(w => w.OrderDate < endExclusive);
        }

        var orders = await query
            .Include(w => w.Client)
            .Include(w => w.Patient)
            .Include(w => w.Details)
            .OrderByDescending(w => w.OrderDate)
            .Take(MaxListRows)
            .ToListAsync();

        var result = orders.Select(w => new WorkOrderSummaryDto
        {
            WorkOrderId = w.WorkOrderId,
            WoeNumber = w.WoeNumber,
            OrderDate = DateTime.SpecifyKind(w.OrderDate, DateTimeKind.Utc),
            ClientName = w.Client?.ClientName ?? string.Empty,
            PatientName = w.Patient?.PatientName ?? string.Empty,
            TestCount = w.Details.Count,
            TotalAmount = w.TotalAmount,
            Status = w.Status
        }).ToList();

        return Ok(result);
    }

    ///Get a saved work order with its line items.
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(WorkOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkOrderResponse>> GetById(int id)
    {
        var order = await LoadOrderAsync(id);
        if (order is null)
        {
            return NotFound(new ApiError(404, $"Work order {id} was not found."));
        }

        return Ok(ToResponse(order));
    }

    /// Submit a WOE. Creates the patient if new, generates a unique WOE number, and saves
    /// the order header + test lines in a single database transaction.
   
    [HttpPost]
    [ProducesResponseType(typeof(WorkOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<WorkOrderResponse>> Create([FromBody] CreateWorkOrderRequest request)
    {
        // ---- 1. Client must exist and be active ----
        var client = await _db.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClientId == request.ClientId && c.IsActive);

        if (client is null)
        {
            ModelState.AddModelError(nameof(request.ClientId), $"Client {request.ClientId} does not exist or is inactive.");
            return ValidationProblem(ModelState);
        }

        // ---- 2. Patient: existing (by id) or new (all fields required) ----
        Patient? existingPatient = null;

        if (request.PatientId.HasValue)
        {
            existingPatient = await _db.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientId == request.PatientId.Value);

            if (existingPatient is null)
            {
                ModelState.AddModelError(nameof(request.PatientId), $"Patient {request.PatientId} does not exist.");
                return ValidationProblem(ModelState);
            }

            if (existingPatient.ClientId != client.ClientId)
            {
                ModelState.AddModelError(nameof(request.PatientId), "The selected patient belongs to a different client.");
                return ValidationProblem(ModelState);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.PatientName))
            {
                ModelState.AddModelError(nameof(request.PatientName), "Patient name is required for a new patient.");
            }

            if (request.Age is null)
            {
                ModelState.AddModelError(nameof(request.Age), "Age is required for a new patient.");
            }

            if (request.Gender is null)
            {
                ModelState.AddModelError(nameof(request.Gender), "Gender is required for a new patient.");
            }

            if (string.IsNullOrWhiteSpace(request.MobileNumber))
            {
                ModelState.AddModelError(nameof(request.MobileNumber), "Mobile number is required for a new patient.");
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }
        }

        // ---- 3. Tests: merge duplicate lines, every id must be an active test ----
        var lines = request.Tests
            .GroupBy(t => t.TestId)
            .Select(g => new { TestId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToList();

        if (lines.Any(l => l.Quantity > 99))
        {
            ModelState.AddModelError(nameof(request.Tests), "Quantity for a single test cannot exceed 99.");
            return ValidationProblem(ModelState);
        }

        var testIds = lines.Select(l => l.TestId).ToList();
        var tests = await _db.TestMasters
            .AsNoTracking()
            .Where(t => testIds.Contains(t.TestId) && t.IsActive)
            .ToDictionaryAsync(t => t.TestId);

        var invalidIds = testIds.Where(id => !tests.ContainsKey(id)).ToList();
        if (invalidIds.Count > 0)
        {
            ModelState.AddModelError(nameof(request.Tests), $"Invalid or inactive test id(s): {string.Join(", ", invalidIds)}.");
            return ValidationProblem(ModelState);
        }

        // ---- 4. Save patient (if new) + order + lines in ONE transaction ----
        // The WOE number is "max today + 1". If two requests pick the same number, the unique index
        // rejects the second one; we roll back and retry with a fresh number.
        var savedOrderId = 0;

        for (var attempt = 1; attempt <= MaxSaveAttempts; attempt++)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                int patientId;

                if (existingPatient is not null)
                {
                    patientId = existingPatient.PatientId;
                }
                else
                {
                    var newPatient = new Patient
                    {
                        // Temporary unique code; replaced by PAT-000001 style code once the identity value is known.
                        PatientCode = "TMP-" + Guid.NewGuid().ToString("N").Substring(0, 12),
                        PatientName = request.PatientName!.Trim(),
                        Age = request.Age!.Value,
                        Gender = request.Gender!.Value,
                        MobileNumber = request.MobileNumber!.Trim(),
                        ClientId = client.ClientId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _db.Patients.Add(newPatient);
                    await _db.SaveChangesAsync();

                    newPatient.PatientCode = $"PAT-{newPatient.PatientId:D6}";
                    await _db.SaveChangesAsync();

                    patientId = newPatient.PatientId;
                }

                var order = new WorkOrder
                {
                    WoeNumber = await NextWoeNumberAsync(),
                    OrderDate = DateTime.UtcNow,
                    ClientId = client.ClientId,
                    PatientId = patientId,
                    Status = "Saved"
                };

                foreach (var line in lines)
                {
                    var test = tests[line.TestId];
                    order.Details.Add(new WorkOrderTestDetail
                    {
                        TestId = test.TestId,
                        Qty = line.Quantity,
                        Rate = test.Rate,               // snapshot of today's rate
                        Amount = test.Rate * line.Quantity
                    });
                }

                order.TotalAmount = order.Details.Sum(d => d.Amount);

                _db.WorkOrders.Add(order);
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                savedOrderId = order.WorkOrderId;
                break;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex) && attempt < MaxSaveAttempts)
            {
                _logger.LogWarning(ex, "Unique key collision while saving work order (attempt {Attempt}); retrying.", attempt);
                await transaction.RollbackAsync();
                _db.ChangeTracker.Clear();
            }
        }

        if (savedOrderId == 0)
        {
            // Not reachable in practice: the last attempt rethrows and the global handler returns a 500.
            throw new InvalidOperationException("Work order could not be saved.");
        }

        var saved = await LoadOrderAsync(savedOrderId);
        var response = ToResponse(saved!);

        return CreatedAtAction(nameof(GetById), new { id = response.WorkOrderId }, response);
    }

    // ------------------------------------------------------------------ helpers

    private Task<WorkOrder?> LoadOrderAsync(int id) =>
        _db.WorkOrders
            .AsNoTracking()
            .Include(w => w.Client)
            .Include(w => w.Patient)
            .Include(w => w.Details)
                .ThenInclude(d => d.Test)
            .FirstOrDefaultAsync(w => w.WorkOrderId == id);

    /// Builds WOE-yyyyMMdd-#### (sequence restarts every day).
    private async Task<string> NextWoeNumberAsync()
    {
        var prefix = "WOE-" + DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-";

        var last = await _db.WorkOrders
            .Where(w => w.WoeNumber.StartsWith(prefix))
            .OrderByDescending(w => w.WoeNumber.Length)
            .ThenByDescending(w => w.WoeNumber)
            .Select(w => w.WoeNumber)
            .FirstOrDefaultAsync();

        var next = 1;
        if (last is not null && int.TryParse(last.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var current))
        {
            next = current + 1;
        }

        return prefix + next.ToString("D4", CultureInfo.InvariantCulture);
    }

    // 2601 = duplicate key in a unique index, 2627 = unique/primary key constraint violation.
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    private static WorkOrderResponse ToResponse(WorkOrder w) => new()
    {
        WorkOrderId = w.WorkOrderId,
        WoeNumber = w.WoeNumber,
        OrderDate = DateTime.SpecifyKind(w.OrderDate, DateTimeKind.Utc),
        ClientId = w.ClientId,
        ClientName = w.Client?.ClientName ?? string.Empty,
        PatientId = w.PatientId,
        PatientCode = w.Patient?.PatientCode ?? string.Empty,
        PatientName = w.Patient?.PatientName ?? string.Empty,
        Age = w.Patient?.Age ?? 0,
        Gender = w.Patient?.Gender.ToString() ?? string.Empty,
        MobileNumber = w.Patient?.MobileNumber ?? string.Empty,
        TotalAmount = w.TotalAmount,
        Status = w.Status,
        Tests = w.Details
            .OrderBy(d => d.WorkOrderTestDetailId)
            .Select(d => new WorkOrderTestResponse
            {
                TestId = d.TestId,
                TestCode = d.Test?.TestCode ?? string.Empty,
                TestName = d.Test?.TestName ?? string.Empty,
                Rate = d.Rate,
                Quantity = d.Qty,
                Amount = d.Amount
            })
            .ToList()
    };
}

using ClientWOE.API.Data;
using ClientWOE.API.DTOs;
using ClientWOE.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClientWOE.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private const int MaxRows = 20;

    private readonly WoeDbContext _db;

    public PatientsController(WoeDbContext db)
    {
        _db = db;
    }

    /// to Search patients by name, code or mobile number, optionally within one client.
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PatientDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PatientDto>>> Search([FromQuery] string? search, [FromQuery] int? clientId)
    {
        var query = _db.Patients.AsNoTracking().AsQueryable();

        if (clientId.HasValue)
        {
            query = query.Where(p => p.ClientId == clientId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                p.PatientName.Contains(term) ||
                p.PatientCode.Contains(term) ||
                p.MobileNumber.Contains(term));
        }

        var patients = await query
            .OrderBy(p => p.PatientName)
            .Take(MaxRows)
            .ToListAsync();

        return Ok(patients.Select(ToDto).ToList());
    }

    ///Get a single patient.
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PatientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatientDto>> GetById(int id)
    {
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.PatientId == id);
        if (patient is null)
        {
            return NotFound(new ApiError(404, $"Patient {id} was not found."));
        }

        return Ok(ToDto(patient));
    }

    private static PatientDto ToDto(Patient p) => new()
    {
        PatientId = p.PatientId,
        PatientCode = p.PatientCode,
        PatientName = p.PatientName,
        Age = p.Age,
        Gender = p.Gender.ToString(),
        MobileNumber = p.MobileNumber,
        ClientId = p.ClientId
    };
}

using ClientWOE.API.Data;
using ClientWOE.API.DTOs;
using ClientWOE.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClientWOE.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestMasterController : ControllerBase
{
    private const int MaxRows = 100;

    private readonly WoeDbContext _db;

    public TestMasterController(WoeDbContext db)
    {
        _db = db;
    }

    ///List active tests, optionally filtered by name, code or category.
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TestDto>>> GetAll([FromQuery] string? search)
    {
        var query = _db.TestMasters.AsNoTracking().Where(t => t.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(t =>
                t.TestName.Contains(term) ||
                t.TestCode.Contains(term) ||
                (t.Category != null && t.Category.Contains(term)));
        }

        var tests = await query
            .OrderBy(t => t.TestName)
            .Take(MaxRows)
            .ToListAsync();

        return Ok(tests.Select(ToDto).ToList());
    }

    ///Get a single test.
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestDto>> GetById(int id)
    {
        var test = await _db.TestMasters.AsNoTracking().FirstOrDefaultAsync(t => t.TestId == id);
        if (test is null)
        {
            return NotFound(new ApiError(404, $"Test {id} was not found."));
        }

        return Ok(ToDto(test));
    }

    private static TestDto ToDto(TestMaster t) => new()
    {
        TestId = t.TestId,
        TestCode = t.TestCode,
        TestName = t.TestName,
        Category = t.Category,
        Rate = t.Rate,
        IsActive = t.IsActive
    };
}

using ClientWOE.API.Data;
using ClientWOE.API.DTOs;
using ClientWOE.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClientWOE.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientsController : ControllerBase
{
    private readonly WoeDbContext _db;

    public ClientsController(WoeDbContext db)
    {
        _db = db;
    }

    /// List active clients
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ClientDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ClientDto>>> GetAll()
    {
        var clients = await _db.Clients
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.ClientName)
            .ToListAsync();

        return Ok(clients.Select(ToDto).ToList());
    }

    ///Get a single client.
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ClientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientDto>> GetById(int id)
    {
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.ClientId == id);
        if (client is null)
        {
            return NotFound(new ApiError(404, $"Client {id} was not found."));
        }

        return Ok(ToDto(client));
    }

    ///Create a client.
    [HttpPost]
    [ProducesResponseType(typeof(ClientDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClientDto>> Create([FromBody] CreateClientRequest request)
    {
        var code = request.ClientCode.Trim().ToUpperInvariant();

        if (await _db.Clients.AnyAsync(c => c.ClientCode == code))
        {
            return Conflict(new ApiError(409, $"A client with code '{code}' already exists."));
        }

        var client = new Client
        {
            ClientCode = code,
            ClientName = request.ClientName.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Clients.Add(client);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = client.ClientId }, ToDto(client));
    }

    private static ClientDto ToDto(Client c) => new()
    {
        ClientId = c.ClientId,
        ClientCode = c.ClientCode,
        ClientName = c.ClientName,
        Phone = c.Phone,
        Address = c.Address,
        IsActive = c.IsActive
    };
}

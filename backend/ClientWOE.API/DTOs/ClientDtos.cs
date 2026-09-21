using System.ComponentModel.DataAnnotations;

namespace ClientWOE.API.DTOs;

public class ClientDto
{
    public int ClientId { get; set; }
    public string ClientCode { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
}

public class CreateClientRequest
{
    [Required, StringLength(20, MinimumLength = 2)]
    public string ClientCode { get; set; } = string.Empty;

    [Required, StringLength(150, MinimumLength = 2)]
    public string ClientName { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }
}

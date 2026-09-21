using System.ComponentModel.DataAnnotations;

namespace ClientWOE.API.Models;

/// Referring client (hospital, clinic, walk-in).
public class Client
{
    public int ClientId { get; set; }

    [Required, StringLength(20)]
    public string ClientCode { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string ClientName { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Patient> Patients { get; set; } = new List<Patient>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}

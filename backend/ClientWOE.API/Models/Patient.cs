using System.ComponentModel.DataAnnotations;

namespace ClientWOE.API.Models;

///A patient registered under one client.
public class Patient
{
    public int PatientId { get; set; }

    [Required, StringLength(20)]
    public string PatientCode { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string PatientName { get; set; } = string.Empty;

    [Range(0, 120)]
    public int Age { get; set; }

    public Gender Gender { get; set; }

    [Required, StringLength(15)]
    public string MobileNumber { get; set; } = string.Empty;

    public int ClientId { get; set; }
    public Client? Client { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}

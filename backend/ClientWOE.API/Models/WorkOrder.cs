using System.ComponentModel.DataAnnotations;

namespace ClientWOE.API.Models;

/// Work Order Entry (WOE) header - one row per submitted order.
public class WorkOrder
{
    public int WorkOrderId { get; set; }

    [Required, StringLength(30)]
    public string WoeNumber { get; set; } = string.Empty;

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public int ClientId { get; set; }
    public Client? Client { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public decimal TotalAmount { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = "Saved";

    public ICollection<WorkOrderTestDetail> Details { get; set; } = new List<WorkOrderTestDetail>();
}

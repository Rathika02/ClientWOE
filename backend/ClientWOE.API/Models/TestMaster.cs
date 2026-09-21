using System.ComponentModel.DataAnnotations;

namespace ClientWOE.API.Models;

///Catalog of laboratory tests and their current rate.
public class TestMaster
{
    public int TestId { get; set; }

    [Required, StringLength(20)]
    public string TestCode { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string TestName { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Category { get; set; }

    [Range(0.01, 1000000)]
    public decimal Rate { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<WorkOrderTestDetail> WorkOrderDetails { get; set; } = new List<WorkOrderTestDetail>();
}

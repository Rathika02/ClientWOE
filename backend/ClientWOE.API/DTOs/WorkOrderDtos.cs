using System.ComponentModel.DataAnnotations;
using ClientWOE.API.Models;

namespace ClientWOE.API.DTOs;


/// Request body for POST /api/workorders.
/// Either send PatientId (existing patient) or the new-patient fields
/// (PatientName, Age, Gender, MobileNumber) - the controller enforces this rule.

public class CreateWorkOrderRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "A client must be selected.")]
    public int ClientId { get; set; }

    public int? PatientId { get; set; }

    [StringLength(100, MinimumLength = 2, ErrorMessage = "Patient name must be 2 to 100 characters.")]
    public string? PatientName { get; set; }

    [Range(0, 120, ErrorMessage = "Age must be between 0 and 120.")]
    public int? Age { get; set; }

    /// 0 = Male, 1 = Female, 2 = Other (names such as "Male" are also accepted).
    public Gender? Gender { get; set; }

    [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Mobile number must be a valid 10-digit number starting with 6-9.")]
    public string? MobileNumber { get; set; }

    [Required(ErrorMessage = "At least one test must be selected.")]
    [MinLength(1, ErrorMessage = "At least one test must be selected.")]
    public List<WorkOrderTestRequest> Tests { get; set; } = new();
}

public class WorkOrderTestRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Each test must have a valid testId.")]
    public int TestId { get; set; }

    [Range(1, 99, ErrorMessage = "Quantity must be between 1 and 99.")]
    public int Quantity { get; set; } = 1;
}

public class WorkOrderResponse
{
    public int WorkOrderId { get; set; }
    public string WoeNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }

    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;

    public int PatientId { get; set; }
    public string PatientCode { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;

    public List<WorkOrderTestResponse> Tests { get; set; } = new();
}

public class WorkOrderTestResponse
{
    public int TestId { get; set; }
    public string TestCode { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
}

/// Lightweight row for GET /api/workorders (list view).
public class WorkOrderSummaryDto
{
    public int WorkOrderId { get; set; }
    public string WoeNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public int TestCount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}

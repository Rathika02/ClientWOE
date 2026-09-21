namespace ClientWOE.API.DTOs;

public class TestDto
{
    public int TestId { get; set; }
    public string TestCode { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public decimal Rate { get; set; }
    public bool IsActive { get; set; }
}

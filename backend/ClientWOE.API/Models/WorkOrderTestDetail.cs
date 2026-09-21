namespace ClientWOE.API.Models;


public class WorkOrderTestDetail
{
    public int WorkOrderTestDetailId { get; set; }

    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }

    public int TestId { get; set; }
    public TestMaster? Test { get; set; }

    public int Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
}

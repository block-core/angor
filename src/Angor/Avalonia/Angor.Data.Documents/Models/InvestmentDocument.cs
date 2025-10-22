namespace Angor.Data.Documents.Models;

public class InvestmentDocument : BaseDocument
{
    public string ProjectId { get; set; } = string.Empty;
    public string InvestorAddress { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string TransactionHash { get; set; } = string.Empty;
    public DateTime InvestmentDate { get; set; }
    public string Status { get; set; } = "Pending";
}
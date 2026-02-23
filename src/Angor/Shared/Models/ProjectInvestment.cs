namespace Angor.Shared.Models;

public class ProjectInvestment
{
    public string TransactionId { get; set; } = string.Empty;
        
    public string InvestorPublicKey { get; set; } = string.Empty;
        
    public long TotalAmount { get; set; }
        
    public string HashOfSecret { get; set; } = string.Empty;

    public bool IsSeeder { get; set; }
}
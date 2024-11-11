namespace Angor.Shared.Models;

public class ProjectInvestment
{
    public string TransactionId { get; set; }
        
    public string InvestorPublicKey { get; set; }
        
    public long TotalAmount { get; set; }
        
    public string HashOfSecret { get; set; }

    public bool IsSeeder { get; set; }
}
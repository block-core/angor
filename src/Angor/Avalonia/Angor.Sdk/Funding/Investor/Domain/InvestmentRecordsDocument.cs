namespace Angor.Sdk.Funding.Investor.Domain;

public class InvestmentRecordsDocument
{
    public required string WalletId { get; set; }
    public List<InvestmentRecord> Investments { get; set; } = new();
}
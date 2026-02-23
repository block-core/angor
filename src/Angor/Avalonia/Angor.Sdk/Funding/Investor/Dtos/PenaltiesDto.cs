namespace Angor.Sdk.Funding.Investor.Dtos;

public record PenaltiesDto()
{
    public string ProjectIdentifier { get; set; } = string.Empty;
    public string InvestorPubKey { get; set; } = string.Empty;
    public long AmountInRecovery { get; set; }
    public long TotalAmountSats { get; set; }
    public bool IsExpired { get; set; }
    public int DaysLeftForPenalty { get; set; }
    public string? ProjectName { get; set; }
}
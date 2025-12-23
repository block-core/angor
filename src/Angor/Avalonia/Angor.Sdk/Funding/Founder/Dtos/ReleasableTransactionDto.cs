namespace Angor.Sdk.Funding.Founder.Dtos;

public record ReleasableTransactionDto
{
    public DateTime? Released { get; init; }
    public required DateTime Arrived { get; init; }
    public required DateTime Approved { get; init; }
    public required string InvestmentEventId { get; init; }
}

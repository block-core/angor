namespace Angor.Contexts.Funding.Founder.Dtos;

public record ReleaseableTransactionDto
{
    public DateTime? Released { get; init; }
    public required DateTime Arrived { get; init; }
    public required DateTime Approved { get; init; }
    public required string InvestmentEventId { get; init; }
}
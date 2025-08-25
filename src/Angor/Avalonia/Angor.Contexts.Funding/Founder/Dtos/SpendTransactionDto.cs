namespace Angor.Contexts.Funding.Founder.Dtos;

public record SpendTransactionDto
{
    public required string InvestorAddress { get; init; }
    public required int StageId { get; init; }
}
namespace Angor.Sdk.Funding.Founder.Dtos;

public record SpendTransactionDto
{
    public required string InvestorAddress { get; init; }
    public required int StageId { get; init; }

    /// <summary>
    /// The stage index within the investment transaction itself (0-based).
    /// Required to spend the correct taproot output when a date bucket mixes
    /// different per-investment stage indices (Fund/Subscribe projects).
    /// </summary>
    public required int InvestmentStageIndex { get; init; }
}
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Projects.Domain;

namespace Angor.Sdk.Funding.Founder.Dtos;

public record ClaimableTransactionDto
{
    public required int StageId { get; init; }
    public required int StageNumber { get; init; }

    /// <summary>
    /// The stage index within the investment transaction itself (0-based).
    /// For Fund/Subscribe projects this can differ from <see cref="StageId"/>:
    /// stages are grouped by release date, and a later investor's first stage
    /// can share a date bucket with an earlier investor's second stage.
    /// This index is required to rebuild the correct taproot script when spending.
    /// </summary>
    public required int InvestmentStageIndex { get; init; }

    public required string InvestorAddress { get; init; }
    public required Amount Amount { get; init; }
    public ClaimStatus ClaimStatus { get; set; }
    public DateTime? DynamicReleaseDate { get; set; }

}
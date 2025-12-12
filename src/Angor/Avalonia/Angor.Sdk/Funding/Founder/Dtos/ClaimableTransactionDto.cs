using Angor.Sdk.Funding.Projects.Domain;

namespace Angor.Sdk.Funding.Founder.Dtos;

public record ClaimableTransactionDto
{
    public required int StageId { get; init; }
    public required int StageNumber { get; init; }
    public required string InvestorAddress { get; init; }
    public required Amount Amount { get; init; }
    public ClaimStatus ClaimStatus { get; set; }
    public DateTime? DynamicReleaseDate { get; set; }

}
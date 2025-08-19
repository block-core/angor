using Angor.Contexts.Funding.Projects.Domain;

namespace Angor.Contexts.Funding.Founder.Dtos;

public record ClaimableTransactionDto
{
    public required int StageId { get; init; }
    public required string InvestorAddress { get; init; }
    public required Amount Amount { get; init; }
    public ClaimStatus ClaimStatus { get; set; }
}
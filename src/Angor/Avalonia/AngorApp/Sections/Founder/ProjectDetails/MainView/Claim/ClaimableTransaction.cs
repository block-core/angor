using Angor.Contexts.Funding.Founder.Dtos;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView.Claim;

public class ClaimableTransaction(ClaimableTransactionDto dto) : ReactiveObject, IClaimableTransaction
{
    public IAmountUI Amount { get; } = new AmountUI(dto.Amount.Sats);
    public string Address { get; } = dto.InvestorAddress;
    public ClaimStatus ClaimStatus { get; } = dto.ClaimStatus;
}
using Angor.Sdk.Funding.Founder.Dtos;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.Transactions;

public class ClaimableTransaction(ClaimableTransactionDto dto) : ReactiveObject, IClaimableTransaction
{
    public IAmountUI Amount { get; } = new AmountUI(dto.Amount.Sats);
    public string Address { get; } = dto.InvestorAddress;
    public int StageId { get; } = dto.StageId;
    public ClaimStatus ClaimStatus { get; } = dto.ClaimStatus;
}

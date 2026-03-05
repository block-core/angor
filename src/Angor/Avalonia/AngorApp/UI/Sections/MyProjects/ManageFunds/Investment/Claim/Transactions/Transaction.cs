using Angor.Sdk.Funding.Founder.Dtos;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim.Transactions;

public class Transaction(ClaimableTransactionDto dto) : ReactiveObject, ITransaction
{
    public IAmountUI Amount { get; } = new AmountUI(dto.Amount.Sats);
    public string Address { get; } = dto.InvestorAddress;
    public int StageId { get; } = dto.StageId;
    public ClaimStatus ClaimStatus { get; } = dto.ClaimStatus;
    public bool IsClaimable => ClaimStatus == ClaimStatus.Unspent;
}

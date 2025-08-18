using Angor.Contexts.Funding.Founder.Dtos;
using ReactiveUI.SourceGenerators;

namespace AngorApp.Sections.Founder.ProjectDetails.ManageFunds;

public partial class ClaimableTransaction : ReactiveObject, IClaimableTransaction
{
    [Reactive]
    private ClaimStatus claimStatus;

    [ObservableAsProperty] private bool isClaimable;

    public ClaimableTransaction(ClaimableTransactionDto dto)
    {
        Amount = new AmountUI(dto.Amount.Sats);
        Address = dto.InvestorAddress;
        ClaimStatus = dto.ClaimStatus;
        isClaimableHelper = this
            .WhenAnyValue(transaction => transaction.ClaimStatus, status => status == ClaimStatus.Unspent)
            .ToProperty(this, x => x.IsClaimable);
    }

    public IAmountUI Amount { get; }

    public string Address { get; }
}
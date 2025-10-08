using DraftModel = Angor.Contexts.Funding.Shared.TransactionDrafts.InvestmentDraft;

namespace AngorApp.Flows.Invest.Draft;

public class InvestmentDraftDesign : IInvestmentDraft
{
    public DraftModel DraftModel { get; } = null!;
    public IAmountUI TransactionFee { get; } = new AmountUI(2100);
    public IAmountUI MinerFee { get; } = new AmountUI(5500);
    public IAmountUI AngorFee { get; } = new AmountUI(84530);
    public Task<Result<Guid>> Confirm()
    {
        throw new NotSupportedException();
    }
}

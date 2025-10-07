using System.Threading.Tasks;

namespace AngorApp.Features.Invest.Draft;

public class InvestmentDraftDesign : IInvestmentDraft
{
    public Angor.Contexts.Funding.Shared.TransactionDrafts.InvestmentDraft DraftModel { get; }
    public IAmountUI TransactionFee { get; } = new AmountUI(2100);
    public IAmountUI MinerFee { get; } = new AmountUI(5500);
    public IAmountUI AngorFee { get; } = new AmountUI(84530);
    public Task<Result<Guid>> Confirm()
    {
        throw new NotSupportedException();
    }
}
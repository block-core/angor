using AngorApp.UI.Flows.InvestV2.Model;
using Zafiro.UI.Navigation;

namespace AngorApp.UI.Flows.InvestV2
{
    public interface IInvestViewModel : IHaveFooter, IHaveHeader
    {
        IObservable<IEnumerable<Breakdown>> StageBreakdowns { get; }
        IObservable<TransactionDetails> Details { get; }
        string ProjectId { get; }
        IEnumerable<IAmountUI> AmountPresets { get; }
        IAmountUI AmountToInvest { get; set; }
    }
}

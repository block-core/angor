using Reactive.Bindings;

namespace AngorApp.UI.Flows.InvestV2.Footer
{
    public interface IFooterViewModel
    {
        IReadOnlyReactiveProperty<IAmountUI> AmountToInvest { get; }
        IEnhancedCommand Invest { get; }
        IAmountUI TotalRaised { get; }
        int StageCount { get; }
    }
}
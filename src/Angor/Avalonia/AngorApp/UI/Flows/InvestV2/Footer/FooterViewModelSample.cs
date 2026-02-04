using Reactive.Bindings;

namespace AngorApp.UI.Flows.InvestV2.Footer
{
    public class FooterViewModelSample : IFooterViewModel
    {
        public IReadOnlyReactiveProperty<IAmountUI> AmountToInvest { get; } =
            new Reactive.Bindings.ReactiveProperty<IAmountUI>(AmountUI.FromBtc(0.5m));

        public IEnhancedCommand Invest { get; }
        public IAmountUI TotalRaised { get; }
        public int StageCount { get; } = 3;
    }
}
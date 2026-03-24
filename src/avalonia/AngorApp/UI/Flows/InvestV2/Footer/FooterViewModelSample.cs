using Reactive.Bindings;

namespace AngorApp.UI.Flows.InvestV2.Footer
{
    public class FooterViewModelSample : IFooterViewModel
    {
        public IReadOnlyReactiveProperty<IAmountUI> AmountToInvest { get; } =
            new Reactive.Bindings.ReactiveProperty<IAmountUI>(AmountUI.FromBtc(0.5m));

        public IEnhancedCommand Invest { get; }
        public IAmountUI TotalRaised { get; }
        public IObservable<int> StageCount { get; } = Observable.Return(3);
        public bool HasPenaltyThreshold { get; } = true;
        public IObservable<bool> IsAbovePenaltyThreshold { get; } = Observable.Return(true);
    }
}
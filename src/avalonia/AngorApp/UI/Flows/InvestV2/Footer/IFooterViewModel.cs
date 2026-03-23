using Reactive.Bindings;

namespace AngorApp.UI.Flows.InvestV2.Footer
{
    public interface IFooterViewModel
    {
        IReadOnlyReactiveProperty<IAmountUI> AmountToInvest { get; }
        IEnhancedCommand Invest { get; }
        IAmountUI TotalRaised { get; }
        IObservable<int> StageCount { get; }
        
        /// <summary>
        /// Whether the project has a penalty threshold configured.
        /// When false, all investments require founder approval.
        /// </summary>
        bool HasPenaltyThreshold { get; }
        
        /// <summary>
        /// Whether the current investment amount is above the penalty threshold.
        /// When above: requires founder approval (SubmitInvestment).
        /// When below: can be published directly without approval.
        /// </summary>
        IObservable<bool> IsAbovePenaltyThreshold { get; }
    }
}
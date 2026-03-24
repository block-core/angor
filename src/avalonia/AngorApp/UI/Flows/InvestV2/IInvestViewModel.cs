using Angor.Shared.Models;
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
        
        /// <summary>
        /// Available funding patterns for Fund/Subscribe projects. Empty for Invest projects.
        /// </summary>
        IEnumerable<DynamicStagePattern> AvailablePatterns { get; }
        
        /// <summary>
        /// The currently selected funding pattern. Null for Invest projects.
        /// </summary>
        DynamicStagePattern? SelectedPattern { get; set; }
        
        /// <summary>
        /// Whether the pattern selector should be visible (true for Fund/Subscribe projects with patterns).
        /// </summary>
        bool ShowPatternSelector { get; }
    }
}

using Angor.Shared.Models;

namespace AngorApp.UI.Flows.Invest.Amount;

public interface IAmountViewModel
{
    public long? Amount { get; set; }
    IEnumerable<Breakdown> StageBreakdowns { get; }
    IObservable<bool> IsValid { get; }
    bool RequiresFounderApproval { get; }

    // New properties for Fund/Subscribe support
    byte? SelectedPatternIndex { get; set; }
    bool RequiresPatternSelection { get; }
    List<DynamicStagePattern> AvailablePatterns { get; }
    ProjectType ProjectType { get; }
}
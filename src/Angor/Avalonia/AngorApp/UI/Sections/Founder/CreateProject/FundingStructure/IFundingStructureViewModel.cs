using ReactiveUI.Validation.Collections;
using Angor.Shared.Models;
using System.Collections.ObjectModel;
using AngorApp.UI.Sections.Founder.CreateProject.Moonshot;

namespace AngorApp.UI.Sections.Founder.CreateProject.FundingStructure;

public interface IFundingStructureViewModel
{
    IObservable<bool> IsValid { get; }
    ProjectType ProjectType { get; set; }
    long? Sats { get; set; }
    DateTime FundingStartDate { get; }

    int? PenaltyDays { get; set; }
    long? PenaltyThreshold { get; set; }

    DateTime? FundingEndDate { get; set; }

    DateTime? ExpiryDate { get; set; }
    
    ObservableCollection<SelectableDynamicStagePattern> SelectableDynamicStagePatterns { get; }
    ObservableCollection<DynamicStagePattern> SelectedPatterns { get; }
    int? PayoutDay { get; set; }
    List<DynamicStagePattern> DynamicStagePatterns { get; }
    
    IAmountUI TargetAmount { get; }
    ICollection<string> Errors { get; }
    
    /// <summary>
    /// Applies imported Moonshot project data to the funding structure.
    /// Sets ProjectType to Fund and populates penaltyThreshold and payoutDay.
    /// </summary>
    /// <param name="moonshotData">The imported Moonshot project data.</param>
    void ApplyMoonshotData(MoonshotProjectData moonshotData);
}

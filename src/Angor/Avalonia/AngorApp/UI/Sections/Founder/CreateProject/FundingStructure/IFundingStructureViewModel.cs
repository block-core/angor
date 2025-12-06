using ReactiveUI.Validation.Collections;
using Angor.Shared.Models;
using System.Collections.ObjectModel;

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
}

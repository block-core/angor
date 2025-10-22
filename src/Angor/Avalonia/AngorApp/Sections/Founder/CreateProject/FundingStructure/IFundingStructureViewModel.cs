using ReactiveUI.Validation.Collections;

namespace AngorApp.Sections.Founder.CreateProject.FundingStructure;

public interface IFundingStructureViewModel
{
    IObservable<bool> IsValid { get; }
    long? Sats { get; set; }
    DateTime FundingStartDate { get; }

    int? PenaltyDays { get; set; }

    DateTime? FundingEndDate { get; set; }

    DateTime? ExpiryDate { get; set; }
    
    IAmountUI TargetAmount { get; }

    IAmountUI PenaltyThreshold { get; }

    IAmountUI MinTargetAmount { get; }

    long? PenaltyThresholdSats { get; set; }

    long? MinTargetAmountSats { get; set; }

    ICollection<string> Errors { get; }
}

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
    IEnumerable<string> Errors { get; }
}

namespace AngorApp.Sections.Founder.CreateProject.FundingStructure;

public interface IFundingStructureViewModel
{
    IObservable<bool> IsValid { get; }
    long? Sats { get; set; }
    DateTime StartDate { get; }

    int? PenaltyDays { get; set; }

    DateTime? EndDate { get; set; }

    DateTime? ExpiryDate { get; set; }
}
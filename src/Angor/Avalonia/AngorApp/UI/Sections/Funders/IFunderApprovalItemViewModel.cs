namespace AngorApp.UI.Sections.Funders;

public interface IFunderApprovalItemViewModel
{
    string ProjectName { get; }
    string AmountText { get; }
    DateTimeOffset Timestamp { get; }

    bool IsApproved { get; }

    IEnhancedCommand Approve { get; }
    IEnhancedCommand Reject { get; }
    IEnhancedCommand Message { get; }
}

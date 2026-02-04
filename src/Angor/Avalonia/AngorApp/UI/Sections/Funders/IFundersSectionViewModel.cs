namespace AngorApp.UI.Sections.Funders;

public interface IFundersSectionViewModel
{
    int SelectedTabIndex { get; set; }

    IEnumerable<IFunderApprovalItemViewModel> Pending { get; }
    IEnumerable<IFunderApprovalItemViewModel> Approved { get; }

    int PendingCount { get; }
    int ApprovedCount { get; }

    bool HasPending { get; }

    bool ShowPendingEmptyState { get; }

    IEnhancedCommand Refresh { get; }

    IEnhancedCommand ApproveAll { get; }
}

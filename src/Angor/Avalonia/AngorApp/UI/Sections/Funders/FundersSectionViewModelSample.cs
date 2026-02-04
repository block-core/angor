namespace AngorApp.UI.Sections.Funders;

using System.Linq;

public class FundersSectionViewModelSample : IFundersSectionViewModel
{
    public int SelectedTabIndex { get; set; }

    public IEnumerable<IFunderApprovalItemViewModel> Pending { get; } =
    [
        new FunderApprovalItemViewModelSample()
    ];

    public IEnumerable<IFunderApprovalItemViewModel> Approved { get; } =
    [
        new FunderApprovalItemViewModelSample
        {
            ProjectName = "Testnet Project Beta",
            AmountText = "0.05000 BTC",
            Timestamp = new DateTimeOffset(2026, 2, 1, 10, 12, 0, TimeSpan.Zero),
            IsApproved = true
        }
    ];

    public int PendingCount => Pending.Count();
    public int ApprovedCount => Approved.Count();

    public bool HasPending => PendingCount > 0;

    public bool ShowPendingEmptyState => !HasPending;

    public IEnhancedCommand Refresh { get; } = ReactiveCommand.Create(() => { }).Enhance();

    public IEnhancedCommand ApproveAll { get; } = ReactiveCommand.Create(() => { }).Enhance();
}

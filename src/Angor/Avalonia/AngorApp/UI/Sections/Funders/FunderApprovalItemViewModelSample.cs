namespace AngorApp.UI.Sections.Funders;

public class FunderApprovalItemViewModelSample : IFunderApprovalItemViewModel
{
    public string ProjectName { get; init; } = "Testnet Project Alpha";
    public string AmountText { get; init; } = "0.10000 BTC";
    public DateTimeOffset Timestamp { get; init; } = new DateTimeOffset(2026, 2, 4, 13, 37, 0, TimeSpan.Zero);

    public bool IsApproved { get; init; }

    public IEnhancedCommand Approve { get; } = ReactiveCommand.Create(() => { }).Enhance();
    public IEnhancedCommand Reject { get; } = ReactiveCommand.Create(() => { }).Enhance();
    public IEnhancedCommand Message { get; } = ReactiveCommand.Create(() => { }).Enhance();
}

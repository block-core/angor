using Zafiro.UI.Commands;

namespace AngorApp.Sections.Portfolio.Manage;

public class InvestorProjectItemDesign : IInvestorProjectItem
{
    public int Stage { get; set; } = 1;
    public IAmountUI Amount { get; set; } = new AmountUI(1234);
    public string Status { get; set; } = "Can claim immediately (no penalty)";

    public IEnhancedCommand<Result> Recover { get; } = ReactiveCommand.Create(() => Result.Success()).Enhance();
    public IEnhancedCommand<Result> Release { get; } = ReactiveCommand.Create(() => Result.Success()).Enhance();
    public IEnhancedCommand<Result> ClaimEndOfProject { get; } = ReactiveCommand.Create(() => Result.Success()).Enhance();
    public IEnhancedCommand<Result> ClaimImmediate { get; } = ReactiveCommand.Create(() => Result.Success()).Enhance();

    public bool ShowRecover { get; } = false;
    public bool ShowRelease { get; } = false;
    public bool ShowClaimEndOfProject { get; } = false;
    public bool ShowClaimImmediate { get; } = true; // Show immediate claim in design mode
}

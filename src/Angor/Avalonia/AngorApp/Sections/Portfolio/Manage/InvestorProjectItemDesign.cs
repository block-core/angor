using Angor.Contexts.Funding.Shared;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Portfolio.Manage;

public class InvestorProjectItemDesign : IInvestorProjectItem
{
    public int Stage { get; set; } = 1;
    public IAmountUI Amount { get; set; } = new AmountUI(1234);
    public string Status { get; set; } = "In Progress";

    public IEnhancedCommand<Result<TransactionDraft>> Recover { get; } = ReactiveCommand.Create(() => Result.Success<TransactionDraft>(default)).Enhance();
    public IEnhancedCommand<Result<TransactionDraft>> Release { get; } = ReactiveCommand.Create(() => Result.Success<TransactionDraft>(default)).Enhance();
    public IEnhancedCommand<Result<TransactionDraft>> ClaimEndOfProject { get; } = ReactiveCommand.Create(() => Result.Success<TransactionDraft>(default)).Enhance();

    public bool ShowRecover { get; } = true;
    public bool ShowRelease { get; } = false;
    public bool ShowClaimEndOfProject { get; } = false;
}

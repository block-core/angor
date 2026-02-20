using AngorApp.UI.Sections.Funders.Chat;
using Angor.Sdk.Funding.Shared;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.UI.Sections.Funders.Items;

public class FunderItem(UIServices uiServices, Func<Task<Result>>? approveOperation = null, Func<Task<Result>>? rejectOperation = null) : IFunderItem
{
    private enum OperationType
    {
        Approve,
        Reject
    }

    private readonly Func<Task<Result>> approveOperation =
        approveOperation ?? (() => Task.FromResult(Result.Failure("Approve operation is not configured.")));

    private readonly Func<Task<Result>> rejectOperation =
        rejectOperation ?? (() => Task.FromResult(Result.Failure("Reject operation is not configured.")));

    public ProjectId ProjectId { get; set; } = new("sample_project_id");
    public string Name { get; set; } = "Default name";
    public IAmountUI Amount { get; set; } = new AmountUI(10000);
    public DateTimeOffset DateCreated { get; set; } = DateTimeOffset.Now;

    public IEnhancedCommand<Result> Approve => EnhancedCommand.CreateWithResult(ApproveCore, Observable.Return(Status == FunderStatus.Pending));

    public IEnhancedCommand<Result> Reject => EnhancedCommand.CreateWithResult(RejectCore, Observable.Return(Status == FunderStatus.Pending));

    public IEnhancedCommand OpenChat =>
        EnhancedCommand.CreateWithResult(() => uiServices.Dialog.Show(new ChatViewModel(InvestorNpub), _ => []));

    public FunderStatus Status { get; set; } = FunderStatus.Pending;
    public string InvestorNpub { get; set; } = "sample_npub";

    private async Task<Result> ApproveCore()
    {
        var confirmation = await uiServices.Dialog.ShowConfirmation("Approve investment", "Do you want to approve this investment?");
        if (confirmation.HasNoValue || !confirmation.Value)
            return Result.Success();

        var result = await approveOperation();
        await ShowResultMessage(result, OperationType.Approve);
        return result;
    }

    private async Task<Result> RejectCore()
    {
        var confirmation = await uiServices.Dialog.ShowConfirmation("Reject investment", "Do you want to reject this investment?");
        if (confirmation.HasNoValue || !confirmation.Value)
            return Result.Success();

        var result = await rejectOperation();
        await ShowResultMessage(result, OperationType.Reject);
        return result;
    }

    private Task ShowResultMessage(Result result, OperationType operation)
    {
        var action = operation switch
        {
            OperationType.Approve => ("approve", "approved"),
            OperationType.Reject => ("reject", "rejected"),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };

        return result.IsSuccess
            ? uiServices.NotificationService.Show($"Investment {action.Item2} successfully.", "Funders")
            : uiServices.NotificationService.Show($"Failed to {action.Item1} investment: {result.Error}", "Funders");
    }
}

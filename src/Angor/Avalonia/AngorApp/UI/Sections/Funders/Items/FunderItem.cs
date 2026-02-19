using AngorApp.UI.Sections.Funders.Chat;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.UI.Sections.Funders.Items;

public class FunderItem(UIServices uiServices) : IFunderItem
{
    public string Name { get; set; } = "Default name";
    public IAmountUI Amount { get; set; } = new AmountUI(10000);
    public DateTimeOffset DateCreated { get; } = DateTimeOffset.Now;

    public IEnhancedCommand<Result> Approve => EnhancedCommand.CreateWithResult(async () =>
    {
        await uiServices.Dialog.ShowMessage("Approve", "Approved successfully");
        return Result.Success();
    }, Observable.Return(Status == FunderStatus.Pending));

    public IEnhancedCommand<Result> Reject =>
        EnhancedCommand.CreateWithResult(async () =>
        {
            await uiServices.Dialog.ShowMessage("Reject", "Rejected successfully");
            return Result.Success();
        }, Observable.Return(Status == FunderStatus.Pending));

    public IEnhancedCommand OpenChat =>
        EnhancedCommand.CreateWithResult(() => uiServices.Dialog.Show(new ChatViewModel(InvestorNpub), _ => []));

    public FunderStatus Status { get; set; } = FunderStatus.Pending;
    public string InvestorNpub { get; set; } = "sample_npub";
}
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Shared;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Release.ReleaseDialog;
using AngorApp.UI.Shared.OperationResult;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Reactive;
using Option = Zafiro.Avalonia.Dialogs.Option;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Release
{
    public class ReleaseViewModel : IReleaseViewModel
    {
        private readonly UIServices uiServices;
        private readonly IFounderAppService founderAppService;
        private readonly IWalletContext walletContext;

        public ReleaseViewModel(IFullProject project, UIServices uiServices, IFounderAppService founderAppService, IWalletContext walletContext)
        {
            this.uiServices = uiServices;
            this.founderAppService = founderAppService;
            this.walletContext = walletContext;
            Project = project;
            ReleaseAll = GetReleaseCommand();
        }

        public IFullProject Project { get; }

        public IEnhancedCommand ReleaseAll { get; }
        
        public bool HasReleasableTransactions => Project.AvailableTransactions > 0;

        private IEnhancedCommand<Unit> GetReleaseCommand()
        {
            return EnhancedCommand.Create(async () =>
            {
                var walletResult = await walletContext.Require();
                if (walletResult.IsFailure)
                {
                    await uiServices.NotificationService.Show(walletResult.Error, "Release Funds");
                    return;
                }

                var wallet = walletResult.Value;

                var releasableResult = await founderAppService.GetReleasableTransactions(
                    new GetReleasableTransactions.GetReleasableTransactionsRequest(wallet.Id, Project.ProjectId));

                if (releasableResult.IsFailure)
                {
                    await uiServices.NotificationService.Show(releasableResult.Error, "Release Funds");
                    return;
                }

                var items = releasableResult.Value.Transactions
                    .Where(t => t.Released is null)
                    .Select(IReleaseDialogItem (dto) => new ReleaseDialogItem(dto))
                    .ToList();

                if (items.Count == 0)
                {
                    await uiServices.NotificationService.Show("No unreleased investments found", "Release Funds");
                    return;
                }

                var dialog = new ReleaseDialogViewModel(items);

                await uiServices.Dialog.Show(
                    dialog,
                    "Release funds",
                    (model, closeable) =>
                    {
                        IEnhancedCommand releaseCommand =
                            EnhancedCommand.Create(
                                () => ReleaseFunds(model, wallet.Id)
                                    .Tap(() => NotifySuccess(model, closeable)));
                        IEnumerable<IOption> options =
                        [
                            new Option(
                                "Cancel",
                                EnhancedCommand.Create(
                                    closeable.Close,
                                    releaseCommand.IsExecuting.Not()),
                                new Zafiro.Avalonia.Dialogs.Settings
                                {
                                    IsCancel = true, Role = OptionRole.Secondary
                                }),
                            new Option(
                                releaseCommand.IsExecuting.Select(b => b
                                    ? "Releasing..."
                                    : "Release Funds"),
                                releaseCommand,
                                new Zafiro.Avalonia.Dialogs.Settings())
                        ];

                        return options;
                    });
            });
        }

        private async Task NotifySuccess(ReleaseDialogViewModel model, ICloseable closeable)
        {
            closeable.Close();
            int releasedCount = model.Selection.SelectionModel.SelectedItems.Count;
            await uiServices.Dialog.Show(
                new OperationResultViewModel(
                    "Funds Released!",
                    $"Successfully released signatures for {releasedCount} investment(s)",
                    new Icon("fa-check")),
                "",
                (_, innerCloseable) =>
                [
                    new Option(
                        "Done",
                        EnhancedCommand.Create(innerCloseable.Close),
                        new Zafiro.Avalonia.Dialogs.Settings { IsCancel = true, Role = OptionRole.Primary })
                ]);
            await uiServices.NotificationService.Show("Funds released successfully!", "Release Funds");
        }

        private async Task<Result> ReleaseFunds(ReleaseDialogViewModel model, WalletId walletId)
        {
            var selectedEventIds = model.Selection.SelectionModel.SelectedItems
                .OfType<IReleaseDialogItem>()
                .Select(item => item.InvestmentEventId)
                .ToList();

            if (selectedEventIds.Count == 0)
                return Result.Failure("No investments selected");

            var result = await founderAppService.ReleaseFunds(
                new ReleaseFunds.ReleaseFundsRequest(walletId, Project.ProjectId, selectedEventIds));

            return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
        }
    }
}
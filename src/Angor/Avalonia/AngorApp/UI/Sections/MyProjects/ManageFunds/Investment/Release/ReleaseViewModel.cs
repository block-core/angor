using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Operations;
using AngorApp.Model.ProjectsV2.InvestmentProject;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Release.ReleaseDialog;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Reactive;
using Option = Zafiro.Avalonia.Dialogs.Option;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Release
{
    public class ReleaseViewModel : IReleaseViewModel
    {
        private readonly UIServices uiServices;
        private readonly IFounderAppService founderAppService;
        private readonly IWalletContext walletContext;

        public ReleaseViewModel(
            IInvestmentProject project,
            IObservable<int> availableTransactions,
            UIServices uiServices,
            IFounderAppService founderAppService,
            IWalletContext walletContext)
        {
            this.uiServices = uiServices;
            this.founderAppService = founderAppService;
            this.walletContext = walletContext;
            Project = project;
            ReleaseAll = GetReleaseCommand();
            HasReleasableTransactions = availableTransactions.Select(count => count > 0).StartWith(false);
        }

        public IInvestmentProject Project { get; }

        public IEnhancedCommand ReleaseAll { get; }
        public IObservable<bool> HasReleasableTransactions { get; }

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
                    new GetReleasableTransactions.GetReleasableTransactionsRequest(wallet.Id, Project.Id));

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
            await uiServices.Dialog.ShowMessage(
                "Funds Released!",
                $"Successfully released signatures for {releasedCount} investment(s)",
                "Done",
                new Icon("fa-check"),
                DialogTone.Success);
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
                new ReleaseFunds.ReleaseFundsRequest(walletId, Project.Id, selectedEventIds));

            return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
        }
    }
}

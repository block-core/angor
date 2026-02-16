using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Shared;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.ClaimDialog;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.Transactions;
using AngorApp.UI.Shared.OperationResult;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Reactive;
using Option = Zafiro.Avalonia.Dialogs.Option;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.Stage
{
    public class ClaimStage : IClaimStage
    {
        private readonly UIServices uiServices;

        public ClaimStage(
            ProjectId projectId,
            int stageId,
            List<IClaimableTransaction> transactions,
            DateTimeOffset claimableOn,
            FundsAvailability fundsAvailability,
            UIServices uiServices
        )
        {
            this.uiServices = uiServices;
            ProjectId = projectId;
            StageId = stageId;
            Transactions = transactions;
            ClaimableOn = claimableOn;
            Claim = CreateClaimCommand();
            FundsAvailability = fundsAvailability;
            ClaimableAmount = new AmountUI(transactions.Sum(transaction => transaction.Amount.Sats));
        }

        public ProjectId ProjectId { get; }

        public IAmountUI ClaimableAmount { get; }

        public DateTimeOffset ClaimableOn { get; }

        public ICollection<IClaimableTransaction> Transactions { get; }

        public IAmountUI TargetAmount { get; } = new AmountUI(1000000);
        public IEnhancedCommand Claim { get; }
        public FundsAvailability FundsAvailability { get; }
        public int StageId { get; }

        private IEnhancedCommand CreateClaimCommand()
        {
            if (CanClaimNow())
            {
                ClaimDialogViewModel dialog = new(
                    "Select UTXOs to Claim",
                    "Select the UTXOs you want to claim. After selecting, click Claim to proceed.",
                    Transactions);
                return EnhancedCommand.Create(
                    () => uiServices.Dialog.Show(
                        dialog,
                        (model, closeable) =>
                        {
                            IOption claimOption = ClaimOption(model, closeable);
                            return
                            [
                                CancelOption(closeable, claimOption.Command.IsExecuting.Not()),
                                claimOption
                            ];
                        }),
                    name: "Available",
                    text: "Claim");
            }

            if (SpentByFounder())
            {
                ClaimDialogViewModel dialog = new(
                    "Spent UTXOs",
                    "These UTXOs have been spent by the founder.",
                    Transactions);
                return EnhancedCommand.Create(
                    () => uiServices.Dialog.Show(dialog, closeable => [CloseOption(closeable)]),
                    name: "Spent",
                    text: "Spent");
            }
            else
            {
                TimeSpan timespan = ClaimableOn - DateTimeOffset.Now;
                ClaimDialogViewModel dialog = new(
                    "Select UTXOs to Claim",
                    $"These UTXOs are available but cannot be claimed yet. Available in {timespan}.",
                    Transactions);
                return EnhancedCommand.Create(
                    () => uiServices.Dialog.Show(dialog, (model, closeable) => [CancelOption(closeable)]),
                    name: "NotReady",
                    text: $"Available in {timespan}");
            }
        }

        private IOption CancelOption(ICloseable closeable, IObservable<bool>? canExecute = null)
        {
            return new Option(
                "Cancel",
                EnhancedCommand.Create(closeable.Close, canExecute),
                new Zafiro.Avalonia.Dialogs.Settings { IsCancel = true, Role = OptionRole.Secondary });
        }

        private IOption CloseOption(ICloseable closeable)
        {
            return new Option(
                "Close",
                EnhancedCommand.Create(closeable.Close),
                new Zafiro.Avalonia.Dialogs.Settings { IsCancel = true, Role = OptionRole.Secondary });
        }

        private IOption ClaimOption(ClaimDialogViewModel model, ICloseable closeable)
        {
            IEnhancedCommand claimCommand = EnhancedCommand.Create(
                () => ClaimFunds().Tap(() => NotifySuccess(closeable)),
                model.SelectedItems.SelectionCount.Select(i => i > 0));
            return new Option(
                claimCommand.IsExecuting.Select(b => b ? "Claiming..." : "Claim"),
                claimCommand,
                new Zafiro.Avalonia.Dialogs.Settings { IsCancel = true });
        }

        private static async Task<Result> ClaimFunds()
        {
            // TODO:
            await Task.Delay(4000);
            return await Task.FromResult(Result.Success());
        }

        private async Task NotifySuccess(ICloseable closeable)
        {
            closeable.Close();
            await uiServices.Dialog.Show(
                new OperationResultViewModel(
                    "Funds Released!",
                    "Successfully released to your investors",
                    new Icon("fa-check")),
                "",
                (_, innerCloseable) =>
                [
                    new Option(
                        "Close",
                        EnhancedCommand.Create(innerCloseable.Close),
                        new Zafiro.Avalonia.Dialogs.Settings { IsCancel = true, Role = OptionRole.Primary })
                ]);
        }

        private bool CanClaimNow()
        {
            return Transactions.Any(transaction => transaction.IsClaimable) && DateTimeOffset.Now >= ClaimableOn;
        }

        private bool SpentByFounder()
        {
            return Transactions.All(transaction => transaction.ClaimStatus == ClaimStatus.SpentByFounder);
        }
    }
}

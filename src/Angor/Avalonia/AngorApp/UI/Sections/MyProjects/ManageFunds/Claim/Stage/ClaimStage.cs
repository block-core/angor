using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.ClaimDialog;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.Transactions;
using AngorApp.UI.Shared.OperationResult;
using AngorApp.UI.TransactionDrafts;
using AngorApp.UI.TransactionDrafts.DraftTypes;
using AngorApp.UI.TransactionDrafts.DraftTypes.Base;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Reactive;
using Option = Zafiro.Avalonia.Dialogs.Option;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.Stage
{
    public class ClaimStage : IClaimStage
    {
        private readonly UIServices uiServices;
        private readonly IFounderAppService founderAppService;
        private readonly IWalletContext walletContext;

        public ClaimStage(
            ProjectId projectId,
            int stageId,
            List<IClaimableTransaction> transactions,
            DateTimeOffset claimableOn,
            FundsAvailability fundsAvailability,
            UIServices uiServices,
            IFounderAppService founderAppService,
            IWalletContext walletContext
        )
        {
            this.uiServices = uiServices;
            this.founderAppService = founderAppService;
            this.walletContext = walletContext;
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

        private IOption ClaimOption(ClaimDialogViewModel dialogModel, ICloseable closeable)
        {
            IEnhancedCommand claimCommand = EnhancedCommand.Create(
                () => ClaimFunds(dialogModel).Tap(() => NotifySuccess(closeable)),
                dialogModel.SelectedItems.SelectionCount.Select(i => i > 0));
            return new Option(
                claimCommand.IsExecuting.Select(b => b ? "Claiming..." : "Claim"),
                claimCommand,
                new Zafiro.Avalonia.Dialogs.Settings { IsCancel = true });
        }

        private async Task<Result> ClaimFunds(ClaimDialogViewModel dialogModel)
        {
            var selectedTransactions = dialogModel.SelectedItems.SelectionModel.SelectedItems
                .OfType<IClaimableTransaction>()
                .ToList();

            if (selectedTransactions.Count == 0)
                return Result.Failure("No transactions selected");

            var spendItems = selectedTransactions
                .Select(t => new SpendTransactionDto
                {
                    InvestorAddress = t.Address,
                    StageId = t.StageId
                })
                .ToList();

            var walletResult = await walletContext.Require();
            if (walletResult.IsFailure)
                return Result.Failure(walletResult.Error);

            var wallet = walletResult.Value;

            var draftPreviewer = new TransactionDraftPreviewerViewModel(
                feerate =>
                {
                    var fee = new FeeEstimation { FeeRate = feerate };
                    return founderAppService.SpendStageFunds(
                            new SpendStageFunds.SpendStageFundsRequest(
                                wallet.Id, ProjectId, fee, spendItems))
                        .Map(ITransactionDraftViewModel (response) =>
                            new TransactionDraftViewModel(response.TransactionDraft, uiServices));
                },
                draftModel =>
                {
                    return founderAppService.SubmitTransactionFromDraft(
                            new PublishFounderTransaction.PublishFounderTransactionRequest(draftModel.Model))
                        .Map(_ => Guid.Empty);
                },
                uiServices);

            var result = await uiServices.Dialog.ShowAndGetResult(
                draftPreviewer,
                "Claim Stage Funds",
                s => s.CommitDraft.Enhance("Claim"));

            return result.HasValue ? Result.Success() : Result.Failure("Cancelled");
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

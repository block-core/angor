using System.Reactive.Disposables;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Shared;
using AngorApp.Model.ProjectsV2;
using AngorApp.Model.Shared.Services;

namespace AngorApp.Model.Funded.Shared.Model;

public class FundedCommandsFactory(
    INotificationService notificationService,
    ITransactionDraftPreviewer draftPreviewer,
    IInvestmentAppService appService,
    IWalletContext walletContext
) : IFundedCommandsFactory
{
    public IFundedCommands Create(IProject project, IInvestorData investorData)
    {
        return new FundedCommands(
            project,
            investorData,
            notificationService,
            draftPreviewer,
            appService,
            walletContext);
    }

    private sealed class FundedCommands : IFundedCommands
    {
        private readonly CompositeDisposable disposables = new();
        private readonly IProject project;
        private readonly IInvestorData investorData;
        private readonly INotificationService notificationService;
        private readonly ITransactionDraftPreviewer draftPreviewer;
        private readonly IInvestmentAppService appService;
        private readonly IWalletContext walletContext;

        public FundedCommands(
            IProject project,
            IInvestorData investorData,
            INotificationService notificationService,
            ITransactionDraftPreviewer draftPreviewer,
            IInvestmentAppService appService,
            IWalletContext walletContext)
        {
            this.project = project;
            this.investorData = investorData;
            this.notificationService = notificationService;
            this.draftPreviewer = draftPreviewer;
            this.appService = appService;
            this.walletContext = walletContext;

            var canCancelApproval =
                investorData.Status.Select(status => status == InvestmentStatus.PendingFounderSignatures);
            var canConfirmInvestment =
                investorData.Status.Select(status => status == InvestmentStatus.FounderSignaturesReceived);
            var canCancelInvestment = canConfirmInvestment;
            var canRecoverFunds = investorData.Status.CombineLatest(
                investorData.RecoveryState,
                (status, recoveryState) =>
                    status == InvestmentStatus.Invested &&
                    (recoveryState.HasUnspentItems || recoveryState.HasSpendableItemsInPenalty));

            OpenChat = EnhancedCommand.CreateWithResult(Result.Success);

            CancelApproval = EnhancedCommand.CreateWithResult(Cancel, canCancelApproval).DisposeWith(disposables);
            CancelInvestment = EnhancedCommand.CreateWithResult(Cancel, canCancelInvestment).DisposeWith(disposables);
            ConfirmInvestment = EnhancedCommand.CreateWithResult(Confirm, canConfirmInvestment).DisposeWith(disposables);
            RecoverFunds = EnhancedCommand.CreateWithResult(Recover, canRecoverFunds).DisposeWith(disposables);

            RecoverFundsLabel = investorData.RecoveryState.Select(state => state.ButtonLabel);
        }

        public IEnhancedCommand<Result> CancelApproval { get; }
        public IEnhancedCommand<Result> CancelInvestment { get; }
        public IEnhancedCommand<Result> ConfirmInvestment { get; }
        public IEnhancedCommand<Result> OpenChat { get; }
        public IEnhancedCommand<Result> RecoverFunds { get; }
        public IObservable<string> RecoverFundsLabel { get; }

        public void Dispose()
        {
            disposables.Dispose();
        }

        private async Task<Result> Cancel()
        {
            return await walletContext.Require()
                .Bind(wallet =>
                {
                    CancelInvestmentRequest.CancelInvestmentRequestRequest cancelInvestmentRequest = new(
                        wallet.Id,
                        project.Id,
                        investorData.InvestmentId);
                    return appService.CancelInvestmentRequest(cancelInvestmentRequest);
                })
                .Tap(() => notificationService.Show(
                    "The investment has been canceled",
                    "Canceled"))
                .TapError(error => notificationService.Show(
                    $"Failed to cancel investment: {error}",
                    "Canceled"));
        }

        private async Task<Result> Confirm()
        {
            return await walletContext.Require()
                .Bind(wallet =>
                {
                    PublishInvestment.PublishInvestmentRequest publishInvestmentRequest = new(
                        investorData.InvestmentId,
                        wallet.Id,
                        project.Id);

                    return appService.ConfirmInvestment(publishInvestmentRequest);
                })
                .Tap(() => notificationService.Show(
                    "The investment has been confirmed",
                    "Confirmed"))
                .TapError(error => notificationService.Show(
                    $"Failed to confirm investment: {error}",
                    "Confirmed"));
        }

        private async Task<Result> Recover()
        {
            var walletResult = await walletContext.Require();
            if (walletResult.IsFailure)
                return walletResult;

            var wallet = walletResult.Value;
            var recoveryState = await investorData.RecoveryState.FirstAsync();

            var (createDraft, commitDraft, title, successMessage) =
                ResolveRecoveryAction(recoveryState, wallet.Id, project.Id);

            if (createDraft == null)
                return Result.Failure("No recovery action available");

            var previewResult = await draftPreviewer.PreviewAndCommit(createDraft, commitDraft!, title!, wallet.Id);

            if (previewResult.HasNoValue)
                return Result.Success();

            return await previewResult.Value
                .Tap(() => notificationService.Show(successMessage!, "Success"));
        }

        private (Func<long, Task<Result<TransactionDraft>>>? CreateDraft, Func<TransactionDraft, Task<Result<Guid>>>? CommitDraft, string? Title, string? SuccessMessage)
            ResolveRecoveryAction(RecoveryState recoveryState, WalletId walletId, ProjectId projectId)
        {
            Func<TransactionDraft, Task<Result<Guid>>> makeCommit() =>
                draft => appService
                    .SubmitTransactionFromDraft(new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(walletId.Value, projectId, draft))
                    .Map(_ => Guid.Empty);

            if (recoveryState is { HasUnspentItems: true, HasReleaseSignatures: true })
            {
                return (
                    feerate => appService.BuildUnfundedReleaseTransaction(new BuildUnfundedReleaseTransaction.BuildUnfundedReleaseTransactionRequest(walletId, projectId, new DomainFeerate(feerate)))
                        .Map(response => (TransactionDraft)response.TransactionDraft),
                    makeCommit(),
                    "Claim Released Funds",
                    "Released funds have been claimed successfully");
            }

            if (recoveryState.HasUnspentItems && (recoveryState.EndOfProject || !recoveryState.IsAboveThreshold))
            {
                var title = recoveryState.IsAboveThreshold ? "Claim Funds" : "Claim Funds (Below Threshold)";
                return (
                    feerate => appService.BuildEndOfProjectClaim(new BuildEndOfProjectClaim.BuildEndOfProjectClaimRequest(walletId, projectId, new DomainFeerate(feerate)))
                        .Map(response => (TransactionDraft)response.TransactionDraft),
                    makeCommit(),
                    title,
                    "Funds claim transaction has been submitted successfully");
            }

            if (recoveryState.HasUnspentItems && !recoveryState.HasSpendableItemsInPenalty)
            {
                return (
                    feerate => appService.BuildRecoveryTransaction(new BuildRecoveryTransaction.BuildRecoveryTransactionRequest(walletId, projectId, new DomainFeerate(feerate)))
                        .Map(response => (TransactionDraft)response.TransactionDraft),
                    makeCommit(),
                    "Recover Funds",
                    "Funds recovery transaction has been submitted successfully");
            }

            if (recoveryState.HasSpendableItemsInPenalty)
            {
                return (
                    feerate => appService.BuildPenaltyReleaseTransaction(new BuildPenaltyReleaseTransaction.BuildPenaltyReleaseTransactionRequest(walletId, projectId, new DomainFeerate(feerate)))
                        .Map(response => (TransactionDraft)response.TransactionDraft),
                    makeCommit(),
                    "Release Funds",
                    "Penalty release transaction has been submitted successfully");
            }

            return (null, null, null, null);
        }
    }
}

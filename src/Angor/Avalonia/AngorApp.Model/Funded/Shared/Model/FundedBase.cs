using System.Reactive.Disposables;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Shared;
using AngorApp.Model.ProjectsV2;
using AngorApp.Model.Shared.Services;
using ReactiveUI;
using Zafiro.Reactive;

namespace AngorApp.Model.Funded.Shared.Model;

public abstract class FundedBase : IFunded, IDisposable
{
    private readonly CompositeDisposable disposables = new();

    protected FundedBase(
        IProject project,
        IInvestorData investorData,
        INotificationService notificationService,
        ITransactionDraftPreviewer draftPreviewer,
        IInvestmentAppService appService,
        IWalletContext walletContext
    )
    {
        Project = project;
        InvestorData = investorData;

        var canCancelApproval =
            investorData.Status.Select(status => status == InvestmentStatus.PendingFounderSignatures);
        var canInvest =
            investorData.Status.Select(status => status == InvestmentStatus.FounderSignaturesReceived);
        var canCancelInvestment = canInvest;
        var canRecoverFunds = investorData.Status.CombineLatest(
            investorData.Recovery,
            (status, r) => status == InvestmentStatus.Invested && (r.HasUnspentItems || r.HasSpendableItemsInPenalty));

        OpenChat = EnhancedCommand.CreateWithResult(Result.Success);

        CancelApproval = EnhancedCommand.CreateWithResult(
            () => DoCancelInvestment(notificationService, appService, walletContext),
            canCancelApproval).DisposeWith(disposables);
        CancelInvestment = EnhancedCommand.CreateWithResult(
            () => DoCancelInvestment(notificationService, appService, walletContext),
            canCancelInvestment).DisposeWith(disposables);
        ConfirmInvestment = EnhancedCommand.CreateWithResult(
            () => DoConfirmInvestment(notificationService, appService, walletContext),
            canInvest).DisposeWith(disposables);
        RecoverFunds = EnhancedCommand.CreateWithResult(
            () => DoRecoverFunds(notificationService, draftPreviewer, appService, walletContext),
            canRecoverFunds).DisposeWith(disposables);

        RecoverFundsLabel = investorData.Recovery.Select(r => r.ButtonLabel);

        var refreshHappened = CancelApproval.Merge(CancelInvestment).Merge(ConfirmInvestment).Merge(RecoverFunds).ToSignal();

        refreshHappened.InvokeCommand(InvestorData.Refresh).DisposeWith(disposables);
    }

    public IProject Project { get; }
    public IInvestorData InvestorData { get; }
    public IEnhancedCommand<Result> CancelApproval { get; }
    public IEnhancedCommand<Result> CancelInvestment { get; }
    public IEnhancedCommand<Result> ConfirmInvestment { get; }
    public IEnhancedCommand<Result> OpenChat { get; }
    public IEnhancedCommand<Result> RecoverFunds { get; }
    public IObservable<string> RecoverFundsLabel { get; }

    private async Task<Result> DoCancelInvestment(
        INotificationService notificationService,
        IInvestmentAppService appService,
        IWalletContext walletContext
    )
    {
        return await walletContext.Require()
                                  .Bind(wallet =>
                                  {
                                      CancelInvestmentRequest.CancelInvestmentRequestRequest
                                          cancelInvestmentRequest = new(
                                              wallet.Id,
                                              Project.Id,
                                              InvestorData.InvestmentId);
                                      return appService.CancelInvestmentRequest(cancelInvestmentRequest);
                                  })
                                  .Tap(() => notificationService.Show(
                                           "The investment has been canceled",
                                           "Canceled"))
                                  .TapError(error => notificationService.Show(
                                                $"Failed to cancel investment: {error}",
                                                "Canceled"));
    }

    private async Task<Result> DoConfirmInvestment(
        INotificationService notificationService,
        IInvestmentAppService appService,
        IWalletContext walletContext
    )
    {
        return await walletContext.Require()
                                  .Bind(wallet =>
                                  {
                                      PublishInvestment.PublishInvestmentRequest publishInvestmentRequest = new(
                                          InvestorData.InvestmentId,
                                          wallet.Id,
                                          Project.Id);

                                      return appService.ConfirmInvestment(publishInvestmentRequest);
                                  })
                                  .Tap(() =>
                                  {
                                      InvestorData.SetStatus(InvestmentStatus.Invested);
                                      notificationService.Show(
                                          "The investment has been confirmed",
                                          "Confirmed");
                                  })
                                  .TapError(error => notificationService.Show(
                                                $"Failed to confirm investment: {error}",
                                                "Confirmed"));
    }

    private async Task<Result> DoRecoverFunds(
        INotificationService notificationService,
        ITransactionDraftPreviewer draftPreviewer,
        IInvestmentAppService appService,
        IWalletContext walletContext
    )
    {
        return await walletContext.Require()
            .Bind(async wallet =>
            {
                var recoveryState = await InvestorData.Recovery.FirstAsync();
                var projectId = Project.Id;

                var (createDraft, commitDraft, title, successMessage) = ResolveRecoveryAction(recoveryState, wallet.Id, projectId, appService);

                if (createDraft == null)
                    return Result.Failure("No recovery action available");

                return await draftPreviewer.PreviewAndCommit(createDraft, commitDraft!, title!, wallet.Id)
                    .Tap(() => notificationService.Show(successMessage!, "Success"));
            });
    }

    private static (Func<long, Task<Result<TransactionDraft>>>? CreateDraft, Func<TransactionDraft, Task<Result<Guid>>>? CommitDraft, string? Title, string? SuccessMessage)
        ResolveRecoveryAction(RecoveryState r, WalletId walletId, ProjectId projectId, IInvestmentAppService appService)
    {
        Func<TransactionDraft, Task<Result<Guid>>> makeCommit() =>
            draft => appService
                .SubmitTransactionFromDraft(new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(walletId.Value, projectId, draft))
                .Map(_ => Guid.Empty);

        if (r.HasUnspentItems && r.HasReleaseSignatures)
        {
            return (
                fr => appService.BuildUnfundedReleaseTransaction(new BuildUnfundedReleaseTransaction.BuildUnfundedReleaseTransactionRequest(walletId, projectId, new DomainFeerate(fr)))
                    .Map(resp => (TransactionDraft)resp.TransactionDraft),
                makeCommit(),
                "Claim Released Funds",
                "Released funds have been claimed successfully");
        }

        if (r.HasUnspentItems && (r.EndOfProject || !r.IsAboveThreshold))
        {
            var title = r.IsAboveThreshold ? "Claim Funds" : "Claim Funds (Below Threshold)";
            return (
                fr => appService.BuildEndOfProjectClaim(new BuildEndOfProjectClaim.BuildEndOfProjectClaimRequest(walletId, projectId, new DomainFeerate(fr)))
                    .Map(resp => (TransactionDraft)resp.TransactionDraft),
                makeCommit(),
                title,
                "Funds claim transaction has been submitted successfully");
        }

        if (r.HasUnspentItems && !r.HasSpendableItemsInPenalty)
        {
            return (
                fr => appService.BuildRecoveryTransaction(new BuildRecoveryTransaction.BuildRecoveryTransactionRequest(walletId, projectId, new DomainFeerate(fr)))
                    .Map(resp => (TransactionDraft)resp.TransactionDraft),
                makeCommit(),
                "Recover Funds",
                "Funds recovery transaction has been submitted successfully");
        }

        if (r.HasSpendableItemsInPenalty)
        {
            return (
                fr => appService.BuildPenaltyReleaseTransaction(new BuildPenaltyReleaseTransaction.BuildPenaltyReleaseTransactionRequest(walletId, projectId, new DomainFeerate(fr)))
                    .Map(resp => (TransactionDraft)resp.TransactionDraft),
                makeCommit(),
                "Release Funds",
                "Penalty release transaction has been submitted successfully");
        }

        return (null, null, null, null);
    }

    public void Dispose()
    {
        disposables.Dispose();
    }
}

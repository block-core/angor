using System.Reactive.Disposables;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Shared.TransactionDrafts;
using AngorApp.TransactionDrafts;
using AngorApp.TransactionDrafts.DraftTypes;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;

namespace AngorApp.Sections.Portfolio.Manage;

public class ManageInvestorProjectViewModel : ReactiveObject, IManageInvestorProjectViewModel, IDisposable
{
    private readonly CompositeDisposable disposables = new();

    private readonly ProjectId projectId;
    private readonly IInvestmentAppService investmentAppService;
    private readonly UIServices uiServices;

    public ManageInvestorProjectViewModel(ProjectId projectId, IInvestmentAppService investmentAppService, UIServices uiServices, IWalletContext walletContext)
    {
        this.projectId = projectId;
        this.investmentAppService = investmentAppService;
        this.uiServices = uiServices;

        ViewTransaction = ReactiveCommand.Create(() => { }).Enhance();
        Load = ReactiveCommand.CreateFromTask(() => walletContext.RequiresWallet(GetRecoveryState)).Enhance().DisposeWith(disposables);
        State = Load.Successes();
        BatchAction = Load.Successes().Select(CreateBatchCommand);
        
        // Refresh on Batch Action completion
        BatchAction.Select(command => (IObservable<Maybe<Guid>>)command).Switch().ToSignal().InvokeCommand(Load).DisposeWith(disposables);
    }

    private IEnhancedCommand<Maybe<Guid>> CreateBatchCommand(RecoveryState recoveryState)
    {
        if (recoveryState.CanRecover)
        {
            return ReactiveCommand.CreateFromTask(() => Recover(recoveryState)).Enhance("Recover Funds");
        }
        
        if (recoveryState.CanRelease)
        {
            return ReactiveCommand.CreateFromTask(() => Release(recoveryState)).Enhance("Release Funds");
        }
        
        if (recoveryState.CanClaim)
        {
            return ReactiveCommand.CreateFromTask(() => Claim(recoveryState)).Enhance("Claim Funds");
        }

        return ReactiveCommand.Create(() => Maybe<Guid>.None, Observable.Return(false)).Enhance();
    }

    public IObservable<RecoveryState> State { get; }

    private Task<Maybe<Guid>> Recover(RecoveryState recoveryState)
    {
        var transactionDraftPreviewerViewModel = new TransactionDraftPreviewerViewModel(fr =>
        {
            return investmentAppService.BuildRecoverInvestorFunds(recoveryState.WalletId.Value, projectId, new DomainFeerate(fr))
                .Map(ITransactionDraftViewModel (draft) => new InvestmentTransactionDraftViewModel((InvestmentDraft)draft, uiServices));
        }, model => investmentAppService.SubmitTransactionFromDraft(recoveryState.WalletId.Value, model.Model)
            .Tap(_ => uiServices.Dialog.ShowOk("Success", "Funds recovery transaction has been submitted successfully"))
            .Map(_ => Guid.Empty), uiServices);

        return uiServices.Dialog.ShowAndGetResult(transactionDraftPreviewerViewModel, "Recover Funds", s => s.CommitDraft.Enhance("Recover Funds"));
    }
    
    private Task<Maybe<Guid>> Claim(RecoveryState recoveryState)
    {
        var transactionDraftPreviewerViewModel = new TransactionDraftPreviewerViewModel(fr =>
        {
            return investmentAppService.BuilodClaimInvestorEndOfProjectFunds(recoveryState.WalletId.Value, projectId, new DomainFeerate(fr))
                .Map(ITransactionDraftViewModel (draft) => new InvestmentTransactionDraftViewModel((InvestmentDraft)draft, uiServices));
        }, model => investmentAppService.SubmitTransactionFromDraft(recoveryState.WalletId.Value, model.Model)
            .Tap(_ => uiServices.Dialog.ShowOk("Success", "Funds claim transaction has been submitted successfully"))
            .Map(_ => Guid.Empty), uiServices);

        return uiServices.Dialog.ShowAndGetResult(transactionDraftPreviewerViewModel, "Recover Funds", s => s.CommitDraft.Enhance("Recover Funds"));
    }
    
    private Task<Maybe<Guid>> Release(RecoveryState recoveryState)
    {
        var transactionDraftPreviewerViewModel = new TransactionDraftPreviewerViewModel(fr =>
        {
            return investmentAppService.BuildReleaseInvestorFunds(recoveryState.WalletId.Value, projectId, new DomainFeerate(fr))
                .Map(ITransactionDraftViewModel (draft) => new InvestmentTransactionDraftViewModel((InvestmentDraft)draft, uiServices));
        }, model => investmentAppService.SubmitTransactionFromDraft(recoveryState.WalletId.Value, model.Model)
            .Tap(_ => uiServices.Dialog.ShowOk("Success", "Funds claim transaction has been submitted successfully"))
            .Map(_ => Guid.Empty), uiServices);

        return uiServices.Dialog.ShowAndGetResult(transactionDraftPreviewerViewModel, "Recover Funds", s => s.CommitDraft.Enhance("Recover Funds"));
    }

    public IObservable<IEnhancedCommand> BatchAction { get; }

    private Task<Result<RecoveryState>> GetRecoveryState(IWallet wallet)
    {
        return investmentAppService
            .GetInvestorProjectRecovery(wallet.Id.Value, projectId)
            .Map(dto => new RecoveryState(wallet.Id, dto));
    }

    public IEnhancedCommand ViewTransaction { get; }
    public IEnhancedCommand<Result<RecoveryState>> Load { get; }

    public void Dispose()
    {
        disposables.Dispose();
    }
}
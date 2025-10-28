using System.Linq;
using System.Reactive.Disposables;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Shared.TransactionDrafts;
using Angor.Contexts.Wallet.Domain;
using AngorApp.TransactionDrafts;
using AngorApp.TransactionDrafts.DraftTypes;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Option = Zafiro.Avalonia.Dialogs.Option;

namespace AngorApp.Sections.Portfolio.Manage;

public partial class ManageInvestorProjectViewModel : ReactiveObject, IManageInvestorProjectViewModel, IDisposable
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

        var loadCommand = ReactiveCommand.CreateFromTask(() => walletContext.RequiresWallet(GetRecoveryState)).Enhance().DisposeWith(disposables);
        loadCommand.HandleErrorsWith(uiServices.NotificationService, "Failed to load recovery info").DisposeWith(disposables);
        Load = loadCommand;
        State = loadCommand.Successes();

        BatchAction = loadCommand.Successes().Select(recoveryState => CreateCommand(recoveryState).Enhance());
    }

    private IEnhancedCommand<Maybe<Guid>> CreateCommand(RecoveryState recoveryState)
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
            .Map(dto => CreateRecoveryViewModel(wallet.Id, dto));
    }

    public IEnhancedCommand ViewTransaction { get; }
    public IEnhancedCommand Load { get; }

    private static RecoveryState CreateRecoveryViewModel(WalletId walletId, InvestorProjectRecoveryDto dto)
    {
        

        return new RecoveryState(walletId, dto);
    }

    public void Dispose()
    {
        disposables.Dispose();
    }
}

public class InvestorProjectStage : ReactiveObject, IInvestorProjectStage
{
    public InvestorProjectStage(int stage, IAmountUI amount, bool isSpent, string status)
    {
        Stage = stage;
        Amount = amount;
        Status = status;
        IsSpent = isSpent;
    }

    public int Stage { get; }
    public IAmountUI Amount { get; }
    public string Status { get; }
    public bool IsSpent { get; }
}

public class InvestedProject : IInvestedProject
{
    public InvestedProject(InvestorProjectRecoveryDto dto)
    {
        TotalFunds = new AmountUI(dto.TotalSpendable);
        ExpiryDate = dto.ExpiryDate;
        PenaltyPeriod = TimeSpan.FromDays(dto.PenaltyDays);
        Name = dto.Name ?? dto.ProjectIdentifier;
    }

    public IAmountUI TotalFunds { get; }
    public DateTime ExpiryDate { get; }
    public TimeSpan PenaltyPeriod { get; }
    public string Name { get; }
}

public sealed record RecoveryState
{
    private readonly InvestorProjectRecoveryDto dto;

    public RecoveryState(WalletId WalletId, InvestorProjectRecoveryDto dto)
    {
        this.dto = dto;
        this.WalletId = WalletId;
        
        Project = new InvestedProject(dto);
        Stages = dto.Items
            .Select(IInvestorProjectStage (x) => new InvestorProjectStage(
                stage: x.StageIndex + 1,
                amount: new AmountUI(x.Amount),
                isSpent: x.IsSpent,
                status: x.Status))
            .ToList();
    }

    public List<IInvestorProjectStage> Stages { get;  }

    public InvestedProject Project { get; }

    public bool CanRecover => dto.CanRecover;
    public bool CanRelease => dto.CanRelease;
    public bool CanClaim => Stages.Any(stage => !stage.IsSpent);
    public WalletId WalletId { get; }
}
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

        var command = loadCommand.Successes().Select(recoveryState => ReactiveCommand.CreateFromTask(() => CreateTransactionDraft(recoveryState)).Enhance());
        Action = command;
    }

    public IObservable<RecoveryState> State { get; }

    private Task<Maybe<Guid>> CreateTransactionDraft(RecoveryState recoveryState)
    {
        if (IsRecovery(recoveryState))
        {
            return Recover(recoveryState);
        }
        
        if (IsClaim(recoveryState))
        {
            return Claim(recoveryState);
        }
        
        if (IsRelease(recoveryState))
        {
            return Release(recoveryState);
        }
        
        throw new ArgumentException("Invalid recoveryState");
    }

    private bool IsRelease(RecoveryState recoveryState)
    {
        return true;
    }

    private bool IsClaim(RecoveryState recoveryState)
    {
        return true;
    }

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

    private bool IsRecovery(RecoveryState recoveryState)
    {
        return true;
    }

    public IObservable<IEnhancedCommand> Action { get; }

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
        var project = new InvestedProject(dto);
        var items = dto.Items
            .Select(IInvestorProjectItem (x) => new InvestorProjectItem(
                stage: x.StageIndex + 1,
                amount: new AmountUI(x.Amount),
                status: x.Status))
            .ToList();

        return new RecoveryState(walletId, project, items);
    }

    public void Dispose()
    {
        disposables.Dispose();
    }

    private class InvestorProjectItem : ReactiveObject, IInvestorProjectItem
    {
        public InvestorProjectItem(int stage, IAmountUI amount, string status)
        {
            Stage = stage;
            Amount = amount;
            Status = status;
        }

        public int Stage { get; }
        public IAmountUI Amount { get; }
        public string Status { get; }
    }

    private class InvestedProject : IInvestedProject
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
}

public sealed record RecoveryState(WalletId WalletId, IInvestedProject Project, IReadOnlyList<IInvestorProjectItem> Items);
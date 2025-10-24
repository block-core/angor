using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Shared;
using Angor.Shared.Models;
using Angor.UI.Model.Implementation.Common;
using ReactiveUI;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;

namespace AngorApp.Sections.Portfolio.Manage;

public partial class ManageInvestorProjectViewModel : ReactiveObject, IManageInvestorProjectViewModel, IDisposable
{
    private readonly CompositeDisposable disposables = new();

    private readonly ProjectId projectId;
    private readonly IInvestmentAppService investmentAppService;
    private RecoveryState state = RecoveryState.Empty;

    public ManageInvestorProjectViewModel(ProjectId projectId, IInvestmentAppService investmentAppService, UIServices uiServices, IWalletContext walletContext)
    {
        this.projectId = projectId;
        this.investmentAppService = investmentAppService;

        ViewTransaction = ReactiveCommand.Create(() => { }).Enhance();

        
        var loadCommand = ReactiveCommand.CreateFromTask(() => walletContext.RequiresWallet(GetRecoveryState)).Enhance().DisposeWith(disposables);
        loadCommand.HandleErrorsWith(uiServices.NotificationService, "Failed to load recovery info").DisposeWith(disposables);
        Load = loadCommand;

        var command = loadCommand.Successes().Select(state => CreateCommand(state));
        this.Action = command;
    }

    public IObservable<IEnhancedCommand> Action { get; set; }

    private IEnhancedCommand<Result> CreateCommand(RecoveryState recoveryState)
    {
        var generateDraft = GetDraft(recoveryState);
        return ReactiveCommand.Create(() => Result.Success()).Enhance("Some action");
    }

    private Maybe<ITransactionDraft> GetDraft(RecoveryState recoveryState)
    {
        throw new NotImplementedException();
    }

    private Task<Result<RecoveryState>> GetRecoveryState(IWallet wallet)
    {
        return investmentAppService
            .GetInvestorProjectRecovery(wallet.Id.Value, projectId)
            .Map(dto => CreateRecoveryViewModel(wallet.Id.Value, dto));
    }

    private async Task<Result> ExecuteDraft(Func<Task<Result<TransactionDraft>>> buildDraft, Guid walletId)
    {
        var draftResult = await buildDraft();
        if (draftResult.IsFailure)
        {
            return Result.Failure(draftResult.Error);
        }

        var submitResult = await investmentAppService.SubmitTransactionFromDraft(walletId, draftResult.Value);
        return submitResult.IsSuccess ? Result.Success() : Result.Failure(submitResult.Error);
    }

    public IAmountUI TotalFunds => Project.TotalFunds;
    public IEnhancedCommand ViewTransaction { get; }
    public IEnhancedCommand<Result> RecoverAll { get; }
    public IEnhancedCommand<Result> ReleaseAll { get; }
    public IEnhancedCommand<Result> ClaimAll { get; }
    public DateTime ExpiryDate => Project.ExpiryDate;
    public TimeSpan PenaltyPeriod => Project.PenaltyPeriod;
    public IEnumerable<IInvestorProjectItem> Items => state.Items;
    public IInvestedProject Project => state.Project;
    public IEnhancedCommand Load { get; }

    // private Task<Result<TransactionDraft>> RecoverAllAction()
    // {
    //     return ExecuteForWallet(id => investmentAppService.BuildRecoverInvestorFunds(id, projectId, new DomainFeerate(1)));
    // }
    //
    // private Task<Result> ReleaseAllAction()
    // {
    //     return ExecuteForWallet(id => investmentAppService.BuildReleaseInvestorFunds(id, projectId, new DomainFeerate(1)));
    // }
    //
    // private Task<Result> ClaimAllAction()
    // {
    //     return ExecuteForWallet(id => investmentAppService.BuilodClaimInvestorEndOfProjectFunds(id, projectId, new DomainFeerate(1)));
    // }

    private Task<Result> ExecuteForWallet(Func<Guid, Task<Result<TransactionDraft>>> buildDraft)
    {
        if (state.WalletId is not Guid walletId)
        {
            return Task.FromResult(Result.Failure("Wallet not available"));
        }

        return ExecuteDraft(() => buildDraft(walletId), walletId);
    }
    private static RecoveryState CreateRecoveryViewModel(Guid walletId, InvestorProjectRecoveryDto dto)
    {
        var project = new InvestedProject(dto);
        var items = dto.Items
            .Select(x => (IInvestorProjectItem)new InvestorProjectItem(
                stage: x.StageIndex + 1,
                amount: new AmountUI(x.Amount),
                status: x.Status))
            .ToList();

        var batchAction = DetermineBatchAction(dto);

        return new RecoveryState(walletId, project, items);
    }

    private static BatchActionMode DetermineBatchAction(InvestorProjectRecoveryDto dto)
    {
        if (dto.EndOfProject && dto.Items.Any(i => !i.IsSpent))
        {
            return BatchActionMode.Claim;
        }

        if (dto.CanRecover && dto.Items.Any(i => !i.IsSpent))
        {
            return BatchActionMode.Recover;
        }

        if (dto.CanRelease && dto.Items.Any(i => i.ScriptType == ProjectScriptTypeEnum.InvestorWithPenalty && !i.IsSpent))
        {
            return BatchActionMode.Release;
        }

        return BatchActionMode.None;
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

    private enum BatchActionMode
    {
        None,
        Recover,
        Release,
        Claim
    }

    private sealed record RecoveryState(Guid? WalletId, IInvestedProject Project, IReadOnlyList<IInvestorProjectItem> Items)
    {
        public static RecoveryState Empty { get; } = new(null, new InvestedProjectDesign(), []);
    }
}

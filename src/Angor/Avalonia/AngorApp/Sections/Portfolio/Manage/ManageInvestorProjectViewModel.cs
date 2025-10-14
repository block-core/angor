using System.Linq;
using System.Reactive.Disposables;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Shared;
using Angor.Shared.Models;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;

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

        var enhancedCommand = ReactiveCommand.CreateFromTask(() => walletContext.RequiresWallet(GetRecoveryData)).Enhance().DisposeWith(disposables);
        Load = enhancedCommand;

        enhancedCommand.HandleErrorsWith(uiServices.NotificationService, "Failed to load recovery info").DisposeWith(disposables);

        RefreshWhenAnyCommandExecutes().DisposeWith(disposables);
    }

    private Task<Result<InvestorProjectRecoveryDto>> GetRecoveryData(IWallet wallet)
    {
        return investmentAppService
            .GetInvestorProjectRecovery(wallet.Id.Value, projectId)
            .Tap(dto => Apply(wallet, dto));
    }

    private void Apply(IWallet wallet, InvestorProjectRecoveryDto dto)
    {
        Project = new InvestedProject(dto);
        Items = dto.Items
            .Select(x =>
            {
                Func<Task<Result>> recover = () => ExecuteDraft(() => investmentAppService.BuildRecoverInvestorFunds(wallet.Id.Value, projectId, new DomainFeerate(1)), wallet.Id.Value); // TODO fee from UI
                Func<Task<Result>> release = () => ExecuteDraft(() => investmentAppService.BuildReleaseInvestorFunds(wallet.Id.Value, projectId, new DomainFeerate(1)), wallet.Id.Value);
                Func<Task<Result>> claim = () => ExecuteDraft(() => investmentAppService.BuilodClaimInvestorEndOfProjectFunds(wallet.Id.Value, projectId, new DomainFeerate(1)), wallet.Id.Value);

                return (IInvestorProjectItem)new InvestorProjectItem(
                    stage: x.StageIndex + 1,
                    amount: new AmountUI(x.Amount),
                    status: x.Status,
                    canRecover: !x.IsSpent && !dto.EndOfProject && dto.CanRecover,
                    canRelease: x.ScriptType == ProjectScriptTypeEnum.InvestorWithPenalty && dto.CanRelease,
                    canClaimEnd: dto.EndOfProject && !x.IsSpent,
                    recoverAction: recover,
                    releaseAction: release,
                    claimAction: claim,
                    notificationService: uiServices.NotificationService);
            })
            .ToList();
        this.RaisePropertyChanged(nameof(Project));
        this.RaisePropertyChanged(nameof(Items));
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
    public DateTime ExpiryDate => Project.ExpiryDate;
    public TimeSpan PenaltyPeriod => Project.PenaltyPeriod;
    public IEnumerable<IInvestorProjectItem> Items { get; private set; } = Array.Empty<IInvestorProjectItem>();
    public IInvestedProject Project { get; private set; } = new InvestedProjectDesign();
    public IEnhancedCommand Load { get; }

    private IDisposable RefreshWhenAnyCommandExecutes()
    {
        return OnRowCommandsExecuted().InvokeCommand(Load);
    }

    private IObservable<Unit> OnRowCommandsExecuted()
    {
        return this.WhenAnyValue(vm => vm.Items)
            .WhereNotNull()
            .Select(items =>
                Observable.Merge(
                    items.Select(i => i.Recover.Successes().ToSignal()).Merge(),
                    items.Select(i => i.Release.Successes().ToSignal()).Merge(),
                    items.Select(i => i.ClaimEndOfProject.Successes().ToSignal()).Merge()
                )
            )
            .Switch();
    }

    public void Dispose()
    {
        disposables.Dispose();
    }

    private class InvestorProjectItem : ReactiveObject, IInvestorProjectItem
    {
        public InvestorProjectItem(int stage, IAmountUI amount, string status, bool canRecover, bool canRelease, bool canClaimEnd, Func<Task<Result>> recoverAction, Func<Task<Result>> releaseAction, Func<Task<Result>> claimAction, INotificationService notificationService)
        {
            Stage = stage;
            Amount = amount;
            Status = status;
            ShowRecover = canRecover;
            ShowRelease = canRelease;
            ShowClaimEndOfProject = canClaimEnd;

            Recover = ReactiveCommand.CreateFromTask(recoverAction, Observable.Return(canRecover)).Enhance();
            Release = ReactiveCommand.CreateFromTask(releaseAction, Observable.Return(canRelease)).Enhance();
            ClaimEndOfProject = ReactiveCommand.CreateFromTask(claimAction, Observable.Return(canClaimEnd)).Enhance();

            Recover.HandleErrorsWith(notificationService, "Failed to recover funds");
            Release.HandleErrorsWith(notificationService, "Failed to release funds");
            ClaimEndOfProject.HandleErrorsWith(notificationService, "Failed to claim funds");

            Recover.Successes()
                .SelectMany(_ => Observable.FromAsync(() => notificationService.Show("Recovery transaction published", Maybe<string>.From("Success"))))
                .Subscribe();
            Release.Successes()
                .SelectMany(_ => Observable.FromAsync(() => notificationService.Show("Release transaction published", Maybe<string>.From("Success"))))
                .Subscribe();
            ClaimEndOfProject.Successes()
                .SelectMany(_ => Observable.FromAsync(() => notificationService.Show("Claim transaction published", Maybe<string>.From("Success"))))
                .Subscribe();
        }

        public int Stage { get; }
        public IAmountUI Amount { get; }
        public string Status { get; }

        public IEnhancedCommand<Result> Recover { get; }
        public IEnhancedCommand<Result> Release { get; }
        public IEnhancedCommand<Result> ClaimEndOfProject { get; }

        public bool ShowRecover { get; }
        public bool ShowRelease { get; }
        public bool ShowClaimEndOfProject { get; }
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

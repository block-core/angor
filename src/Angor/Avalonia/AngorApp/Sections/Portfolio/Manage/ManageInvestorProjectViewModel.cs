using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Shared;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;
using Zafiro.UI.Commands;
using Zafiro.Reactive;
using Angor.Shared.Models;

namespace AngorApp.Sections.Portfolio.Manage;

public partial class ManageInvestorProjectViewModel : ReactiveObject, IManageInvestorProjectViewModel, IDisposable
{
    private readonly CompositeDisposable disposables = new();

    private readonly ProjectId projectId;
    private readonly IInvestmentAppService investmentAppService;
    private readonly UIServices uiServices;

    [ObservableAsProperty]
    private IWallet wallet;

    public ManageInvestorProjectViewModel(ProjectId projectId, IInvestmentAppService investmentAppService, UIServices uiServices)
    {
        this.projectId = projectId;
        this.investmentAppService = investmentAppService;
        this.uiServices = uiServices;

        ViewTransaction = ReactiveCommand.Create(() => { }).Enhance();

        // Load wallet once and expose it as a property
        LoadWallet = ReactiveCommand.CreateFromTask(() => uiServices.WalletRoot.TryDefaultWalletAndActivate("You need to create a wallet first.")).Enhance().DisposeWith(disposables);
        LoadWallet.HandleErrorsWith(uiServices.NotificationService, "Failed to load wallet").DisposeWith(disposables);
        walletHelper = LoadWallet.Successes().ToProperty(this, x => x.Wallet).DisposeWith(disposables);

        var hasWallet = this.WhenAnyValue(x => x.Wallet).NotNull();

        // Load recovery info using the current wallet
        Load = ReactiveCommand.CreateFromTask(
            () => investmentAppService
                .GetInvestorProjectRecovery(Wallet.Id.Value, projectId)
                .Tap(Apply),
            hasWallet).Enhance().DisposeWith(disposables);

        Load.HandleErrorsWith(uiServices.NotificationService, "Failed to load recovery info").DisposeWith(disposables);

        // Auto-load on create
        LoadWallet.Execute().Subscribe().DisposeWith(disposables);
        LoadWallet.ToSignal().InvokeCommand(Load).DisposeWith(disposables);

        // Refresh when any command executes (row commands)
        RefreshWhenAnyCommandExecutes().DisposeWith(disposables);
    }

    private void Apply(InvestorProjectRecoveryDto dto)
    {
        var currentWallet = Wallet; // Wallet is guaranteed by hasWallet

        Project = new InvestedProject(dto);
        Items = dto.Items
            .Select(x =>
            {
                Func<Task<Result<TransactionDraft>>> recover = () =>
                    investmentAppService.BuildRecoverInvestorFunds(currentWallet.Id.Value, projectId, new DomainFeerate(1)); //TODO Jose get the fee rate from the UI (in satoshis per byte)
                Func<Task<Result<TransactionDraft>>> release = () =>
                    investmentAppService.BuildReleaseInvestorFunds(currentWallet.Id.Value, projectId,new DomainFeerate(1)); //TODO Jose get the fee rate from the UI (in satoshis per byte)
                Func<Task<Result<TransactionDraft>>> claim = () =>
                    investmentAppService.BuilodClaimInvestorEndOfProjectFunds(currentWallet.Id.Value, projectId, new DomainFeerate(1)); //TODO Jose get the fee rate from the UI (in satoshis per byte)

                return new InvestorProjectItem(
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
            .Cast<IInvestorProjectItem>()
            .ToList();
        this.RaisePropertyChanged(nameof(Project));
        this.RaisePropertyChanged(nameof(Items));
    }

    public IAmountUI TotalFunds => Project.TotalFunds;
    public IEnhancedCommand ViewTransaction { get; }
    public DateTime ExpiryDate => Project.ExpiryDate;
    public TimeSpan PenaltyPeriod => Project.PenaltyPeriod;

    public IEnumerable<IInvestorProjectItem> Items { get; private set; } = Array.Empty<IInvestorProjectItem>();

    public IInvestedProject Project { get; private set; } = new InvestedProjectDesign();

    public IEnhancedCommand<Result<InvestorProjectRecoveryDto>> Load { get; }

    public IEnhancedCommand<Result<IWallet>> LoadWallet { get; }

    private IDisposable RefreshWhenAnyCommandExecutes()
    {
        return OnRowCommandsExecuted().InvokeCommand(Load);
    }

    private IObservable<Unit> OnRowCommandsExecuted()
    {
        return this.WhenAnyValue(vm => vm.Items)
            .WhereNotNull()
            .Select(items => items.Cast<InvestorProjectItem>())
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
        public InvestorProjectItem(int stage, IAmountUI amount, string status, bool canRecover, bool canRelease, bool canClaimEnd, Func<Task<Result<TransactionDraft>>> recoverAction, Func<Task<Result<TransactionDraft>>> releaseAction, Func<Task<Result<TransactionDraft>>> claimAction, INotificationService notificationService)
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

        public IEnhancedCommand<Result<TransactionDraft>> Recover { get; }
        public IEnhancedCommand<Result<TransactionDraft>> Release { get; }
        public IEnhancedCommand<Result<TransactionDraft>> ClaimEndOfProject { get; }

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

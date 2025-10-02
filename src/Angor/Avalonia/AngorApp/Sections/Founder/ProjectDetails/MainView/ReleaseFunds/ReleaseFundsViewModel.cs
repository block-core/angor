using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using AngorApp.Extensions;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView.ReleaseFunds;

public partial class ReleaseFundsViewModel : ReactiveObject, IReleaseFundsViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = new();

    private readonly IInvestmentAppService investmentAppService;

    private readonly ProjectId projectId;

    private readonly UIServices uiServices;

    [ObservableAsProperty] private IEnumerable<IUnfundedProjectTransaction> transactions;

    public ReleaseFundsViewModel(ProjectId projectId, IInvestmentAppService investmentAppService, UIServices uiServices)
    {
        this.projectId = projectId;
        this.investmentAppService = investmentAppService;
        this.uiServices = uiServices;

        RefreshTransactions = WalletCommand.Create(wallet => investmentAppService.GetReleaseableTransactions(wallet.Id.Value, projectId)
                .MapEach(IUnfundedProjectTransaction (dto) => new UnfundedProjectTransaction(wallet.Id.Value, projectId, dto, investmentAppService, uiServices))
                .Map(enumerable => enumerable.ToList()), uiServices.WalletRoot)
            .DisposeWith(disposable);

        transactionsHelper = RefreshTransactions
            .Successes()
            .ToProperty(this, model => model.Transactions)
            .DisposeWith(disposable);

        ReleaseAll = WalletCommand.Create(DoReleaseAll, uiServices.WalletRoot, this.WhenAnyValue(model => model.Transactions).NotNull())
            .Enhance()
            .DisposeWith(disposable);
        
        RefreshWhenAnyCommandExecutes().DisposeWith(disposable);
        RefreshTransactions.Execute().Subscribe().DisposeWith(disposable);
    }

    public ReactiveCommand<Unit, Result<List<IUnfundedProjectTransaction>>> RefreshTransactions { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }

    public IEnhancedCommand<Maybe<Result>> ReleaseAll { get; }

    private IDisposable RefreshWhenAnyCommandExecutes()
    {
        return new[]
        {
            OnReleaseAllExecuted(),
            OnChildReleaseCommandExecuted()
        }.Merge().InvokeCommand(RefreshTransactions);
    }

    private IObservable<Unit> OnReleaseAllExecuted()
    {
        return ReleaseAll.Values().ToSignal();
    }

    private IObservable<Unit> OnChildReleaseCommandExecuted()
    {
        return this.WhenAnyValue(model => model.Transactions)
            .WhereNotNull()
            .Select(enumerable => enumerable.Select(transaction => transaction.Release.Values()).Merge().ToSignal())
            .Switch();
    }

    private Task<Maybe<Result>> DoReleaseAll(IWallet wallet)
    {
        var addresses = Transactions.Select(transaction => transaction.InvestmentEventId);
        
        return UserFlow.PromptAndNotify(
            () => investmentAppService.ReleaseInvestorTransactions(wallet.Id.Value, projectId, addresses), uiServices,
            "Are you sure you want to release all the funds?",
            "Confirm Release All",
            "Successfully released all",
            "Released All", e => $"Cannot release the funds {e}");
    }
}
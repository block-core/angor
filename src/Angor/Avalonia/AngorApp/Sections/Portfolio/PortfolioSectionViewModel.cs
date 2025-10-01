using System.Linq;
using System.Reactive.Disposables;
using System.Windows.Input;
using Angor.Contexts.Funding.Investor;
using AngorApp.Sections.Portfolio.Items;
using AngorApp.Sections.Portfolio.Penalties;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Portfolio;

public partial class PortfolioSectionViewModel : ReactiveObject, IPortfolioSectionViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = new();
    
    [ObservableAsProperty]
    private IWallet wallet;
    
    [ObservableAsProperty] private IEnumerable<IPortfolioProjectViewModel> investedProjects;
    [ObservableAsProperty] private IInvestorStatsViewModel investorStats;

    public PortfolioSectionViewModel(IInvestmentAppService investmentAppService, UIServices uiServices, INavigator navigator)
    {
        LoadWallet = ReactiveCommand.CreateFromTask(() => uiServices.WalletRoot.TryDefaultWalletAndActivate("You need to create a wallet before having a Portfolio")).Enhance().DisposeWith(disposable);
        LoadWallet.HandleErrorsWith(uiServices.NotificationService, "Failed to load wallet").DisposeWith(disposable);
        walletHelper = LoadWallet.Successes().ToProperty(this, x => x.Wallet).DisposeWith(disposable);

        var hasWallet = this.WhenAnyValue(model => model.Wallet).NotNull();

        investorStatsHelper = this.WhenAnyValue(model => model.InvestedProjects)
            .WhereNotNull()
            .Select(IInvestorStatsViewModel (projects) => new InvestorStatsViewModel(projects.ToList())).ToProperty(this, x => x.InvestorStats).DisposeWith(disposable);
        
        LoadPortfolio = ReactiveCommand.CreateFromTask(() => investmentAppService.GetInvestorProjects(Wallet.Id.Value).MapEach(IPortfolioProjectViewModel (dto) => new PortfolioProjectViewModel(dto, investmentAppService, uiServices, navigator)), hasWallet).DisposeWith(disposable).Enhance();
        LoadPortfolio.HandleErrorsWith(uiServices.NotificationService, "Failed to load portfolio projects").DisposeWith(disposable);
        investedProjectsHelper = LoadPortfolio.Successes().ToProperty(this, x => x.InvestedProjects).DisposeWith(disposable);
        
        LoadWallet.ToSignal().InvokeCommand(LoadPortfolio).DisposeWith(disposable);

        GoToPenalties = ReactiveCommand.Create(navigator.Go<IPenaltiesViewModel>);

        IsLoading = LoadWallet.IsExecuting.CombineLatest(LoadPortfolio.IsExecuting).Select(tuple => tuple.AnyTrue());
    }

    public IEnhancedCommand<Result<IEnumerable<IPortfolioProjectViewModel>>> LoadPortfolio { get; }
    public IObservable<bool> IsLoading { get; }


    public IEnhancedCommand<Result<IWallet>> LoadWallet { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }

    public ICommand GoToPenalties { get; }
}

public static class TupleExtensions
{
    public static bool AllTrue(this ValueTuple<bool, bool> tuple)
    {
        return tuple.ToEnumerable().All(b => b);
    }
    
    public static bool AnyTrue(this ValueTuple<bool, bool> tuple)
    {
        return tuple.ToEnumerable().Any(b => b);
    }


    public static IEnumerable<T> ToEnumerable<T>(this ValueTuple<T, T> tuple)
    {
        yield return tuple.Item1;
        yield return tuple.Item2;
    }
}
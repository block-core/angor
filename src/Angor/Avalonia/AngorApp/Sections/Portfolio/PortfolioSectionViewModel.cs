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
    
    [ObservableAsProperty] private IEnumerable<IPortfolioProject> investedProjects;
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
        
        LoadPortfolio = ReactiveCommand.CreateFromTask(() => investmentAppService.GetInvestorProjects(Wallet.Id.Value).MapEach(IPortfolioProject (dto) => new PortfolioProjectViewModel(dto, investmentAppService, uiServices, navigator)), hasWallet).DisposeWith(disposable).Enhance();
        LoadPortfolio.HandleErrorsWith(uiServices.NotificationService, "Failed to load portfolio projects").DisposeWith(disposable);
        investedProjectsHelper = LoadPortfolio.Successes().ToProperty(this, x => x.InvestedProjects).DisposeWith(disposable);
        
        LoadWallet.Execute().Subscribe().DisposeWith(disposable);

        LoadWallet.ToSignal().InvokeCommand(LoadPortfolio).DisposeWith(disposable);

        GoToPenalties = ReactiveCommand.Create(navigator.Go<IPenaltiesViewModel>);
    }

    public IEnhancedCommand<Result<IEnumerable<IPortfolioProject>>> LoadPortfolio { get; }


    public IEnhancedCommand<Result<IWallet>> LoadWallet { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }

    public ICommand GoToPenalties { get; }
}
using System.Linq;
using System.Reactive.Disposables;
using System.Windows.Input;
using Angor.Contexts.Funding.Investor;
using AngorApp.Sections.Portfolio.Items;
using AngorApp.Sections.Portfolio.Penalties;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Portfolio;

public partial class PortfolioSectionViewModel : ReactiveObject, IPortfolioSectionViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = new();
    
    [ObservableAsProperty] private ICollection<IPortfolioProjectViewModel> investedProjects;
    [ObservableAsProperty] private IInvestorStatsViewModel investorStats;

    public PortfolioSectionViewModel(IInvestmentAppService investmentAppService, UIServices uiServices, INavigator navigator, IWalletContext walletContext)
    {
        investorStatsHelper = this.WhenAnyValue(model => model.InvestedProjects)
            .WhereNotNull()
            .Select(IInvestorStatsViewModel (projects) => new InvestorStatsViewModel(projects.ToList())).ToProperty(this, x => x.InvestorStats).DisposeWith(disposable);

        Func<IWallet, Task<Result<ICollection<IPortfolioProjectViewModel>>>> execute = wallet => GetInvestedProjects(investmentAppService, uiServices, navigator, walletContext, wallet);
        LoadPortfolio = ReactiveCommand.CreateFromTask(() => walletContext.RequiresWallet(execute), null).Enhance();
        LoadPortfolio.HandleErrorsWith(uiServices.NotificationService, "Failed to load portfolio projects").DisposeWith(disposable);
        investedProjectsHelper = LoadPortfolio.Successes().ToProperty(this, x => x.InvestedProjects).DisposeWith(disposable);
        
        GoToPenalties = ReactiveCommand.Create(navigator.Go<IPenaltiesViewModel>);

        IsLoading = LoadPortfolio.IsExecuting;
    }

    private static Task<Result<ICollection<IPortfolioProjectViewModel>>> GetInvestedProjects(IInvestmentAppService investmentAppService, UIServices uiServices, INavigator navigator, IWalletContext walletContext, IWallet wallet)
    {
        return investmentAppService.GetInvestorProjects(wallet.Id.Value)
            .MapEach(IPortfolioProjectViewModel (dto) => new PortfolioProjectViewModel(dto, investmentAppService, uiServices, navigator, walletContext))
            .Map<IEnumerable<IPortfolioProjectViewModel>, ICollection<IPortfolioProjectViewModel>>(models => models.ToList());
    }

    public IEnhancedCommand<Result<ICollection<IPortfolioProjectViewModel>>> LoadPortfolio { get; }
    public IObservable<bool> IsLoading { get; }


    public void Dispose()
    {
        disposable.Dispose();
    }

    public ICommand GoToPenalties { get; }
}
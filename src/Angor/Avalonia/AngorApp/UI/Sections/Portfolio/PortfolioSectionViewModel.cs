using System.Linq;
using System.Reactive.Disposables;
using System.Windows.Input;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using AngorApp.Core;
using AngorApp.UI.Sections.Portfolio.Items;
using AngorApp.UI.Sections.Portfolio.Penalties;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI.Navigation;
using Zafiro.UI.Shell.Utils;

namespace AngorApp.UI.Sections.Portfolio;

[Section("Funded", icon: "fa-arrow-trend-up", sortIndex: 3)]
[SectionGroup("INVESTOR")]
public partial class PortfolioSectionViewModel : ReactiveObject, IPortfolioSectionViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = new();
    
    [ObservableAsProperty] private ICollection<IPortfolioProjectViewModel> investedProjects;
    [ObservableAsProperty] private IInvestorStatsViewModel investorStats;

    public PortfolioSectionViewModel(IInvestmentAppService investmentAppService, UIServices uiServices, INavigator navigator, IWalletContext walletContext, SharedCommands sharedCommands)
    {
        investorStatsHelper = this.WhenAnyValue(model => model.InvestedProjects)
            .WhereNotNull()
            .Select(IInvestorStatsViewModel (projects) => new InvestorStatsViewModel(projects.ToList())).ToProperty(this, x => x.InvestorStats).DisposeWith(disposable);

        Func<IWallet, Task<Result<ICollection<IPortfolioProjectViewModel>>>> execute = wallet => GetInvestedProjects(investmentAppService, uiServices, navigator, walletContext, wallet, sharedCommands);
        LoadPortfolio = ReactiveCommand.CreateFromTask(() => walletContext.RequiresWallet(execute), null).Enhance();
        LoadPortfolio.HandleErrorsWith(uiServices.NotificationService, "Failed to load portfolio projects").DisposeWith(disposable);
        investedProjectsHelper = LoadPortfolio.Successes().ToProperty(this, x => x.InvestedProjects).DisposeWith(disposable);
        
        GoToPenalties = ReactiveCommand.Create(navigator.Go<IPenaltiesViewModel>);

        // Loads the Portfolio whenever CancelInvestment is executed on children
        LoadPortfolio.Successes()
            .Select(projects => projects.Select(project => project.CancelInvestment).Merge())
            .Switch()
            .ToSignal()
            .InvokeCommand(LoadPortfolio)
            .DisposeWith(disposable);

        IsLoading = LoadPortfolio.IsExecuting;
    }

    private static Task<Result<ICollection<IPortfolioProjectViewModel>>> GetInvestedProjects(IInvestmentAppService investmentAppService, UIServices uiServices, INavigator navigator, IWalletContext walletContext, IWallet wallet, SharedCommands sharedCommands)
    {
        return investmentAppService.GetInvestorProjects(new Investments.InvestmentsPortfolioRequest(wallet.Id))
    .Map(response => response.Projects)
       .MapEach(IPortfolioProjectViewModel (dto) => new PortfolioProjectViewModel(dto, investmentAppService, uiServices, navigator, walletContext, sharedCommands))
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
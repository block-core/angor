using System.Reactive.Disposables;
using System.Windows.Input;
using Angor.Contexts.Funding.Investor;
using AngorApp.Sections.Portfolio.Items;
using AngorApp.Sections.Portfolio.Penalties;
using AngorApp.UI.Services;
using DynamicData;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Portfolio;

public class PortfolioSectionViewModel : ReactiveObject, IPortfolioSectionViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = new();

    public PortfolioSectionViewModel(IInvestmentAppService investmentAppService, UIServices uiServices, INavigator navigator)
    {
        var reactiveCommand = ReactiveCommand.CreateFromTask(() =>
        {
            var bind = uiServices.WalletRoot.GetDefaultWalletAndActivate()
                .Bind(maybeWallet => maybeWallet.ToResult("No wallet found. Please, create or import a wallet.")
                    .Bind(wallet => investmentAppService.GetInvestorProjects(wallet.Id.Value)));
            return bind;
        }).DisposeWith(disposable);

        Load = reactiveCommand.Enhance();

        Load.Successes()
            .EditDiff(project => project.Id)
            .Transform(IPortfolioProject (project) => new PortfolioProjectViewModel(project, investmentAppService, uiServices, navigator))
            .Bind(out var investedProjects)
            .Subscribe().DisposeWith(disposable);

        Load.Execute().Subscribe().DisposeWith(disposable);

        GoToPenalties = ReactiveCommand.Create(() => navigator.Go<IPenaltiesViewModel>());

        InvestedProjects = investedProjects;
    }

    public void Dispose()
    {
        disposable.Dispose();
    }

    public IEnhancedCommand<Result<IEnumerable<InvestedProjectDto>>> Load { get; }
    public int FundedProjects { get; } = 123;
    public IAmountUI TotalInvested { get; } = new AmountUI(1000000);
    public IAmountUI RecoveredToPenalty { get; } = new AmountUI(240000);
    public int ProjectsInRecovery { get; } = 6;
    public IEnumerable<IPortfolioProject> InvestedProjects { get; }
    public ICommand GoToPenalties { get; }
}
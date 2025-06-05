using System.Reactive.Disposables;
using Angor.Contexts.Funding.Investor;
using AngorApp.UI.Services;
using Avalonia.Controls.ApplicationLifetimes;
using DynamicData;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Portfolio;

public class PortfolioSectionViewModel : ReactiveObject, IPortfolioSectionViewModel
{
    private readonly CompositeDisposable disposable = new();
    
    public PortfolioSectionViewModel(IInvestmentAppService investmentAppService, UIServices uiServices)
    {
        Items =
        [
            new PortfolioItem("Ariton", "0"),
            new PortfolioItem("Total invested", "0 TBTC"),
            new PortfolioItem("Wallet", "0 TBTC"),
            new PortfolioItem("In Recovery", "0 TBTC"),
        ];

        var reactiveCommand = ReactiveCommand.CreateFromTask(() =>
        {
            var bind = uiServices.WalletRoot.GetDefaultWalletAndActivate()
                .Bind(maybeWallet => maybeWallet.ToResult("No wallet found. Please, create or recover a wallet.")
                    .Bind(wallet => investmentAppService.GetInvestorProjects(wallet.Id.Value)));
            return bind;
        }).DisposeWith(disposable);
        
        Load = reactiveCommand.Enhance();

        Load.Successes()
            .EditDiff(project => project.Id)
            .Transform(IInvestedProject (project) => new InvestedProject(project))
            .Bind(out var investedProjects)
            .Subscribe();
        
        InvestedProjects = investedProjects;
    }

    public IEnhancedCommand<Result<IEnumerable<InvestedProjectDto>>> Load { get; }

    public IReadOnlyCollection<PortfolioItem> Items { get; }
    public IEnumerable<IInvestedProject> InvestedProjects { get; }
}
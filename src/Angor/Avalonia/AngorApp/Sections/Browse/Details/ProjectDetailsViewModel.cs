using System.Threading.Tasks;
using Angor.Contexts.Funding.Investor;
using Angor.UI.Model.Implementation.Projects;
using AngorApp.Features.Invest;
using AngorApp.UI.Controls.Common.FoundedProjectOptions;
using AngorApp.UI.Services;
using Avalonia.Threading;
using Zafiro.Avalonia.Dialogs;
using Zafiro.UI;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Browse.Details;

public class ProjectDetailsViewModel : ReactiveObject, IProjectDetailsViewModel
{
    private readonly FullProject project;
    private readonly InvestWizard investWizard;
    private readonly UIServices uiServices;

    public ProjectDetailsViewModel(FullProject project, InvestWizard investWizard, UIServices uiServices, IInvestmentAppService investmentAppService, INavigator navigator)
    {
        this.project = project;
        this.investWizard = investWizard;
        this.uiServices = uiServices;

        IsInsideInvestmentPeriod = DateTime.Now <= project.FundingEndDate;
        Invest = ReactiveCommand.CreateFromTask(DoInvest, Observable.Return(IsInsideInvestmentPeriod)).Enhance();
        Invest.HandleErrorsWith(uiServices.NotificationService, "Investment failed");
        FoundedProjectOptions = new FoundedProjectOptionsViewModel(project.ProjectId, investmentAppService, uiServices, navigator);
    }

    public bool IsInsideInvestmentPeriod { get; }
    public TimeSpan? NextRelease { get; }
    public IStage? CurrentStage { get; }
    public IFoundedProjectOptionsViewModel FoundedProjectOptions { get; }

    public IEnhancedCommand<Result> Invest { get; }

    public IEnumerable<INostrRelay> Relays { get; } =
    [
        new NostrRelayDesign
        {
            Uri = new Uri("wss://relay.angor.io")
        },
        new NostrRelayDesign
        {
            Uri = new Uri("wss://relay2.angor.io")
        }
    ];

    public IFullProject Project => project;

    private async Task<Result> DoInvest()
    {
        var getCurrentResult = await uiServices.WalletRoot.GetDefaultWalletAndActivate()
            .Tap(r => r.ExecuteNoValue(ShowNoWalletMessage));

        return await getCurrentResult
            .Map(maybeWallet => maybeWallet
                .Bind(wallet => investWizard.Invest(wallet, project)));
    }

    private Task<Maybe<Unit>> ShowNoWalletMessage()
    {
        return Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await uiServices.Dialog.ShowMessage("No wallet found", "Please create or import a wallet to invest in this project.");
            return Maybe<Unit>.None;
        });
    }
}
using System.Reactive.Disposables;
using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using AngorApp.Sections.Portfolio.Manage;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Portfolio.Items;

public partial class PortfolioProjectViewModel : ReactiveObject, IPortfolioProjectViewModel, IDisposable
{
    [Reactive] private bool isInvestmentCompleted;
    [Reactive] private InvestmentStatus investmentStatus;
    private readonly InvestedProjectDto projectDto;
    private readonly CompositeDisposable disposable = new();

    public PortfolioProjectViewModel(InvestedProjectDto projectDto, IInvestmentAppService investmentAppService, UIServices uiServices, INavigator navigator)
    {
        this.projectDto = projectDto;

        var canCompleteInvestment = this.WhenAnyValue(x => x.InvestmentStatus).Select(x => x == InvestmentStatus.FounderSignaturesReceived);

        CompleteInvestment = ReactiveCommand.CreateFromTask(() => investmentAppService.ConfirmInvestment(projectDto.InvestmentId, uiServices.ActiveWallet.Current.Value.Id.Value, new ProjectId(projectDto.Id)), canCompleteInvestment)
            .Enhance()
            .DisposeWith(disposable);

        CompleteInvestment
            .HandleErrorsWith(uiServices.NotificationService, "Failed to complete investment")
            .DisposeWith(disposable);

        CompleteInvestment.Successes()
            .SelectMany(async _ =>
            {
                InvestmentStatus = InvestmentStatus.Invested;
                await uiServices.Dialog.ShowMessage("Investment completed", $"Your investment in \"{projectDto.Name}\" has been completed.");
                return Unit.Default;
            })
            .Subscribe()
            .DisposeWith(disposable);

        Invested = new AmountUI(projectDto.Investment.Sats);

        InvestmentStatus = projectDto.InvestmentStatus;
        GoToManageFunds = ReactiveCommand.CreateFromTask(() => navigator.Go(() => new ManageInvestorProjectViewModel(new ProjectId(projectDto.Id), investmentAppService, uiServices))).Enhance().DisposeWith(disposable);
    }

    public string Name => projectDto.Name;
    public string Description => projectDto.Description;
    public IAmountUI Target => new AmountUI(projectDto.Target.Sats);
    public IAmountUI Raised => new AmountUI(projectDto.Raised.Sats);
    public IAmountUI InRecovery => new AmountUI(projectDto.InRecovery.Sats);
    public FounderStatus FounderStatus => FounderStatus.Approved;
    public Uri LogoUri => projectDto.LogoUri;
    public IEnhancedCommand<Result> CompleteInvestment { get; }
    public IAmountUI Invested { get; }
    public IEnhancedCommand GoToManageFunds { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }
}
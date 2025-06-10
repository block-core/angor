using System.Reactive.Disposables;
using Angor.Contexts.Funding.Investor;
using AngorApp.UI.Services;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Portfolio.Items;

public partial class PortfolioProject : ReactiveObject, IPortfolioProject
{
    private readonly InvestedProjectDto projectDto;
    private readonly CompositeDisposable disposable = new();

    public PortfolioProject(InvestedProjectDto projectDto, IInvestmentAppService investmentAppService, UIServices uiServices)
    {
        this.projectDto = projectDto;
        CompleteInvestment = ReactiveCommand.CreateFromTask(() => investmentAppService.ConfirmInvestment(1234), CanCompleteInvestment).Enhance().DisposeWith(disposable);
        CompleteInvestment.Successes().Do(_ => IsInvestmentCompleted = true).Subscribe().DisposeWith(disposable);
        CompleteInvestment.Successes().Select(a => uiServices.Dialog.ShowMessage("Investment completed", "The investment has been completed")).Subscribe().DisposeWith(disposable);
        IsInvestmentCompleted = projectDto.IsInvesmentCompleted;
    }

    public string Name => projectDto.Name;
    public string Description => projectDto.Description;
    public IAmountUI Target => new AmountUI(projectDto.Target.Sats);
    public IAmountUI Raised => new AmountUI(projectDto.Raised.Sats);
    public IAmountUI InRecovery => new AmountUI(projectDto.InRecovery.Sats);
    public ProjectStatus Status => ProjectStatus.Funding;
    public FounderStatus FounderStatus => FounderStatus.Approved;
    public Uri LogoUri => projectDto.LogoUri;
    public IEnhancedCommand<Result> CompleteInvestment { get; }
    private IObservable<bool> CanCompleteInvestment => this.WhenAnyValue<PortfolioProject, bool>(project => project.IsInvestmentCompleted).Not();
    [ReactiveUI.SourceGenerators.Reactive] private bool isInvestmentCompleted;
}
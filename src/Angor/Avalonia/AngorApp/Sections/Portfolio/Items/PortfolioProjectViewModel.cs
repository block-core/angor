using System.Reactive.Disposables;
using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Investor;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Portfolio.Items;

public partial class PortfolioProjectViewModel : ReactiveObject, IPortfolioProject, IDisposable
{
    [Reactive] private bool isInvestmentCompleted;
    [Reactive] private InvestmentStatus investmentStatus;
    private readonly InvestedProjectDto projectDto;
    private readonly CompositeDisposable disposable = new();
    
    public PortfolioProjectViewModel(InvestedProjectDto projectDto, IInvestmentAppService investmentAppService, UIServices uiServices)
    {
        this.projectDto = projectDto;
        
        var canCompleteInvestment = this.WhenAnyValue(x => x.InvestmentStatus).Select(x => x == InvestmentStatus.FounderSignaturesReceived);
        
        CompleteInvestment = ReactiveCommand.CreateFromTask(() => investmentAppService.ConfirmInvestment(1234), canCompleteInvestment)
            .Enhance()
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
        
        InvestmentStatus = projectDto.InvestmentStatus;
    }

    public string Name => projectDto.Name;
    public string Description => projectDto.Description;
    public IAmountUI Target => new AmountUI(projectDto.Target.Sats);
    public IAmountUI Raised => new AmountUI(projectDto.Raised.Sats);
    public IAmountUI InRecovery => new AmountUI(projectDto.InRecovery.Sats);
    public FounderStatus FounderStatus => FounderStatus.Approved;
    public Uri LogoUri => projectDto.LogoUri;
    public IEnhancedCommand<Result> CompleteInvestment { get; }
    public IAmountUI Invested { get; } = new AmountUI(1234000);

    public void Dispose()
    {
        disposable.Dispose();
        CompleteInvestment.Dispose();
    }
}
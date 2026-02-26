using System.Reactive.Disposables;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Shared;
using AngorApp.Core;
using AngorApp.UI.Sections.Portfolio.Manage;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Navigation;

namespace AngorApp.UI.Sections.Portfolio.Items;

public partial class PortfolioProjectViewModel : ReactiveObject, IPortfolioProjectViewModel, IDisposable
{
    [Reactive] private bool isInvestmentCompleted;
    [Reactive] private InvestmentStatus investmentStatus;
    private readonly InvestedProjectDto projectDto;
    private readonly CompositeDisposable disposable = new();

    public PortfolioProjectViewModel(InvestedProjectDto projectDto, IInvestmentAppService investmentAppService, UIServices uiServices, INavigator navigator, IWalletContext walletContext, SharedCommands sharedCommands)
    {
        this.projectDto = projectDto;

        var canCompleteInvestment = this.WhenAnyValue(x => x.InvestmentStatus).Select(x => x == InvestmentStatus.FounderSignaturesReceived);
        var canCancelInvestment = this.WhenAnyValue(x => x.InvestmentStatus).Select(x => x != InvestmentStatus.Invested);

        CompleteInvestment = ReactiveCommand.CreateFromTask(() => walletContext.Require().Bind(wallet => 
            investmentAppService.ConfirmInvestment(new PublishInvestment.PublishInvestmentRequest(projectDto.InvestmentId, wallet.Id, new ProjectId(projectDto.Id)))
        .Map(_ => Unit.Default)), canCompleteInvestment)
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
        GoToManageFunds = ReactiveCommand.CreateFromTask(() => navigator.Go(() => new ManageInvestorProjectViewModel(new ProjectId(projectDto.Id), investmentAppService, uiServices, walletContext, sharedCommands))).Enhance().DisposeWith(disposable);

        // cancel investment command
        CancelInvestment = ReactiveCommand.CreateFromTask(() => 
                investmentAppService.CancelInvestmentRequest(new Angor.Sdk.Funding.Investor.Operations.CancelInvestmentRequest.CancelInvestmentRequestRequest(walletContext.CurrentWallet.Value.Id, new ProjectId(projectDto.Id), projectDto.InvestmentId))
            .Map(_ => Unit.Default), canCancelInvestment)
            .Enhance()
            .DisposeWith(disposable);

        CancelInvestment
          .HandleErrorsWith(uiServices.NotificationService, "Failed to cancel investment")
          .DisposeWith(disposable);

        CancelInvestment.Successes()
           .SelectMany(async _ =>
           {
               await uiServices.Dialog.ShowMessage("Investment canceled", $"Your investment in \"{projectDto.Name}\" has been canceled.");
               return Unit.Default;
           })
           .Subscribe()
           .DisposeWith(disposable);
    }

    public string Name => projectDto.Name;
    public string Description => projectDto.Description;
    public IAmountUI Target => new AmountUI(projectDto.Target.Sats);
    public IAmountUI Raised => new AmountUI(projectDto.Raised.Sats);
    public IAmountUI InRecovery => new AmountUI(projectDto.InRecovery.Sats);
    public FounderStatus FounderStatus => FounderStatus.Approved;
    public Uri LogoUri => projectDto.LogoUri;
    public IEnhancedCommand<Result> CompleteInvestment { get; }
    public IEnhancedCommand<Result> CancelInvestment { get; }

    public IAmountUI Invested { get; }
    public IEnhancedCommand GoToManageFunds { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }
}

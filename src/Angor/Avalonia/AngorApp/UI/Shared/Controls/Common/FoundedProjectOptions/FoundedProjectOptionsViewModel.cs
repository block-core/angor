using System;
using System.Reactive.Linq;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Shared;
using AngorApp.Core;
using AngorApp.UI.Sections.Portfolio;
using AngorApp.UI.Sections.Portfolio.Items;
using AngorApp.UI.Shared.Services;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI;
using Zafiro.UI.Navigation;

namespace AngorApp.UI.Shared.Controls.Common.FoundedProjectOptions;

public class FoundedProjectOptionsViewModel : IFoundedProjectOptionsViewModel
{
    private readonly ProjectId projectId;
    private readonly IInvestmentAppService investmentAppService;

    public FoundedProjectOptionsViewModel(ProjectId projectId, IInvestmentAppService investmentAppService, UIServices uiServices, INavigator navigator, IWalletContext walletContext, SharedCommands sharedCommands)
    {
        this.projectId = projectId;
        this.investmentAppService = investmentAppService;
        LoadInvestment = ReactiveCommand.CreateFromTask(() => walletContext.RequiresWallet(GetInvestedProject));

        ProjectInvestment = LoadInvestment.Successes().Values()
            .Select(dto => new PortfolioProjectViewModel(dto, investmentAppService, uiServices, navigator, walletContext, sharedCommands));
        
        // This updates the investment info when the user completes an investment
        ProjectInvestment.Select(model => model.CompleteInvestment)
            .Switch()
            .ToSignal()
            .InvokeCommand(LoadInvestment);
    }

    private Task<Result<Maybe<InvestedProjectDto>>> GetInvestedProject(IWallet wallet)
    {
        return investmentAppService.GetInvestments(new GetInvestments.GetInvestmentsRequest(wallet.Id))
            .Map(response => response.Projects.TryFirst(dto => dto.Id == projectId.Value));
    }

    public IObservable<IPortfolioProjectViewModel> ProjectInvestment { get; }
    public ReactiveCommand<Unit, Result<Maybe<InvestedProjectDto>>> LoadInvestment { get; set; }
}

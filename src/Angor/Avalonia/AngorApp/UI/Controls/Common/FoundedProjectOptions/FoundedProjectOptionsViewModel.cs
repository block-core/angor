using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using AngorApp.Extensions;
using AngorApp.Sections.Portfolio;
using AngorApp.Sections.Portfolio.Items;
using AngorApp.UI.Services;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;

namespace AngorApp.UI.Controls.Common.FoundedProjectOptions;

public class FoundedProjectOptionsViewModel : IFoundedProjectOptionsViewModel
{
    public FoundedProjectOptionsViewModel(ProjectId projectProjectId, IInvestmentAppService investmentAppService, UIServices uiServices, INavigator navigator)
    {
        LoadInvestment = WalletCommand.Create(wallet =>
        {
            return investmentAppService.GetInvestorProjects(wallet.Id.Value)
                .Map(dtos => dtos.TryFirst(dto => dto.Id == projectProjectId.Value));
        }, uiServices.WalletRoot);

        ProjectInvestment = LoadInvestment.Successes().Values()
            .Select(dto => new PortfolioProjectViewModel(dto, investmentAppService, uiServices, navigator));
        
        // This updates the investment info when the user completes an investment
        ProjectInvestment.Select(model => model.CompleteInvestment)
            .Switch()
            .ToSignal()
            .InvokeCommand(LoadInvestment);
    }

    public IObservable<IPortfolioProjectViewModel> ProjectInvestment { get; }
    public ReactiveCommand<Unit, Result<Maybe<InvestedProjectDto>>> LoadInvestment { get; set; }
}
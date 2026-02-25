using System.Reactive.Linq;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using AngorApp.UI.Sections.Funded.Manage;
using AngorApp.UI.Sections.Shared;
using Zafiro.UI.Navigation;

namespace AngorApp.UI.Sections.Funded.ProjectList.Item;

public class FundedProjectItem : IFundedProjectItem, IDisposable
{
    public FundedProjectItem(
        ProjectDto projectDto,
        InvestedProjectDto investedProjectDto,
        IInvestmentAppService investmentAppService,
        IProjectAppService projectAppService,
        IWalletContext walletContext,
        Func<IFundedProject, IManageViewModel> manageFactory,
        INavigator navigator)
    {
        Project = new ProjectItem(projectDto, projectAppService);
        Investment = new InvestmentItem(investedProjectDto, investmentAppService, walletContext);
        Manage = EnhancedCommand.Create(() => navigator.Go(() => manageFactory(this)));
    }

    public IProjectItem Project { get; }
    public IInvestmentItem Investment { get; }
    public IEnhancedCommand Manage { get; }

    public void Dispose()
    {
        (Project as IDisposable)?.Dispose();
        (Investment as IDisposable)?.Dispose();
    }
}

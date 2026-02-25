using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Operations;
using AngorApp.UI.Sections.Funded.Manage;
using AngorApp.UI.Sections.Funded.ProjectList.Item;
using Zafiro.CSharpFunctionalExtensions;
using AngorApp.UI.Shell;
using Zafiro.UI.Navigation;
using Zafiro.UI.Shell.Utils;
using ProjectId = Angor.Sdk.Funding.Shared.ProjectId;

namespace AngorApp.UI.Sections.Funded;

[Section("Funded", icon: "fa-arrow-trend-up", sortIndex: 3)]
[SectionGroup("INVESTOR")]
public class FundedSectionViewModel : IFundedSectionViewModel
{
    private readonly IInvestmentAppService investmentAppService;
    private readonly IProjectAppService projectAppService;
    private readonly IWalletContext walletContext;
    private readonly Func<IFundedProject, IManageViewModel> manageFactory;
    private readonly INavigator navigator;

    public FundedSectionViewModel(
        IShellViewModel shell,
        IInvestmentAppService investmentAppService,
        IProjectAppService projectAppService,
        IWalletContext walletContext,
        Func<IFundedProject, IManageViewModel> manageFactory,
        INavigator navigator)
    {
        this.investmentAppService = investmentAppService;
        this.projectAppService = projectAppService;
        this.walletContext = walletContext;
        this.manageFactory = manageFactory;
        this.navigator = navigator;

        FindProjects = EnhancedCommand.Create(() => shell.SetSection("Find Projects"));

        RefreshableCollection<IFundedProjectItem, string> fundedProjects = RefreshableCollection.Create(GetItems, GetItemKey);
        FundedProjects = fundedProjects.Items;
        Refresh = fundedProjects.Refresh;
    }

    public IEnhancedCommand FindProjects { get; }
    public IReadOnlyCollection<IFundedProjectItem> FundedProjects { get; }
    public IEnhancedCommand Refresh { get; }

    private async Task<Result<IEnumerable<IFundedProjectItem>>> GetItems()
    {
        return await walletContext
            .Require()
            .Bind(wallet => investmentAppService.GetInvestments(new GetInvestments.GetInvestmentsRequest(wallet.Id)))
            .Map(response => response.Projects)
            .MapSequentially(GetItem);
    }

    private Task<Result<IFundedProjectItem>> GetItem(InvestedProjectDto dto)
    {
        return projectAppService
            .Get(new GetProject.GetProjectRequest(new ProjectId(dto.Id)))
            .Map(IFundedProjectItem (response) => new FundedProjectItem(response.Project, dto, investmentAppService, projectAppService, walletContext, manageFactory, navigator));
    }

    private static string GetItemKey(IFundedProjectItem item)
    {
        return item.Project.Id.Value;
    }
}

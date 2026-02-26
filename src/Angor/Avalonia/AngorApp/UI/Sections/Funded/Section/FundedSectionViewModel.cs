using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Operations;
using AngorApp.UI.Sections.Funded.Manage;
using AngorApp.UI.Sections.FundedV2.Manage;
using AngorApp.UI.Sections.Shared.New;
using AngorApp.UI.Shell;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Navigation;
using Zafiro.UI.Shell.Utils;
using IManageViewModel = AngorApp.UI.Sections.Funded.Manage.IManageViewModel;
using ProjectId = Angor.Sdk.Funding.Shared.ProjectId;

namespace AngorApp.UI.Sections.Funded.Section;

[Section("Funded", icon: "fa-arrow-trend-up", sortIndex: 3)]
[SectionGroup("INVESTOR")]
public class FundedSectionViewModel : IFundedSectionViewModel, IDisposable
{
    private readonly IInvestmentAppService investmentAppService;
    private readonly IProjectAppService projectAppService;
    private readonly IWalletContext walletContext;
    private readonly INavigator navigator;

    public FundedSectionViewModel(
        IShellViewModel shell,
        IInvestmentAppService investmentAppService,
        IProjectAppService projectAppService,
        IWalletContext walletContext,
        Func<IFundedProject, IManageViewModel> manageFactory,
        INavigator navigator
    )
    {
        this.investmentAppService = investmentAppService;
        this.projectAppService = projectAppService;
        this.walletContext = walletContext;
        this.navigator = navigator;
        FindProjects = EnhancedCommand.Create(() => shell.SetSection("Find Projects"));

        RefreshableCollection<IFundedItem2, string> fundedProjects = RefreshableCollection.Create(GetItems, GetItemKey);
        FundedItems = fundedProjects.Items;
        var refresh = fundedProjects.Refresh;
        Refresh = refresh;
    }

    public IEnhancedCommand FindProjects { get; }
    public IReadOnlyCollection<IFundedItem2> FundedItems { get; }
    public IEnhancedCommand Refresh { get; }


    private async Task<Result<IEnumerable<IFundedItem2>>> GetItems()
    {
        return await walletContext
                     .Require()
                     .Bind(wallet => investmentAppService.GetInvestments(
                               new GetInvestments.GetInvestmentsRequest(wallet.Id)))
                     .Map(response => response.Projects)
                     .MapSequentially(GetItem);
    }

    private Task<Result<IFundedItem2>> GetItem(InvestedProjectDto dto)
    {
        return projectAppService
               .Get(new GetProject.GetProjectRequest(new ProjectId(dto.Id)))
               .Map(IFundedItem2 (response) =>
               {
                   IFunded2 funded = new Funded2(Project2.Create(response.Project), new InvestmentInvestorData2Sample());
                   var manage = EnhancedCommand.Create(() => navigator.Go(() => new Manage2ViewModel(funded)));
                   
                   FundedItem2 fundedItem2 = new(funded, manage);
                   return fundedItem2;
               });
    }

    private static string GetItemKey(IFundedItem2 item)
    {
        return item.Funded.Project.Id.Value;
    }

    public void Dispose()
    {
        FindProjects.Dispose();
        Refresh.Dispose();
    }
}
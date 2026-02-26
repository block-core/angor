using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Operations;
using Angor.Sdk.Funding.Shared;
using AngorApp.UI.Sections.FundedV2.Common.Model;
using AngorApp.UI.Sections.FundedV2.Fund.Model;
using AngorApp.UI.Sections.FundedV2.Host.Manage;
using AngorApp.UI.Sections.FundedV2.Investment.Model;
using AngorApp.UI.Sections.Shared.ProjectV2;
using AngorApp.UI.Shell;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Navigation;
using Zafiro.UI.Shell.Utils;

namespace AngorApp.UI.Sections.FundedV2.Host.Section
{
    [Section("Funded", icon: "fa-arrow-trend-up", sortIndex: 3)]
    [SectionGroup("INVESTOR")]
    public class FundedSectionViewModel : IFundedSectionViewModel, IDisposable
    {
        private readonly IInvestmentAppService investmentAppService;
        private readonly IProjectAppService projectAppService;
        private readonly IWalletContext walletContext;
        private readonly INavigator navigator;
        private readonly UIServices uiServices;

        public FundedSectionViewModel(
            IShellViewModel shell,
            IInvestmentAppService investmentAppService,
            IProjectAppService projectAppService,
            IWalletContext walletContext,
            INavigator navigator, UIServices uiServices
        )
        {
            this.investmentAppService = investmentAppService;
            this.projectAppService = projectAppService;
            this.walletContext = walletContext;
            this.navigator = navigator;
            this.uiServices = uiServices;
            FindProjects = EnhancedCommand.Create(() => shell.SetSection("Find Projects"));

            RefreshableCollection<IFundedItem, string> fundedProjects = RefreshableCollection.Create(GetItems, GetItemKey);
            FundedItems = fundedProjects.Items;
            var refresh = fundedProjects.Refresh;
            Refresh = refresh;
        }

        public IEnhancedCommand FindProjects { get; }
        public IReadOnlyCollection<IFundedItem> FundedItems { get; }
        public IEnhancedCommand Refresh { get; }


        private async Task<Result<IEnumerable<IFundedItem>>> GetItems()
        {
            return await walletContext
                         .Require()
                         .Bind(wallet => investmentAppService.GetInvestments(
                                   new GetInvestments.GetInvestmentsRequest(wallet.Id)))
                         .Map(response => response.Projects)
                         .MapSequentially(GetItem);
        }

        private Task<Result<IFundedItem>> GetItem(InvestedProjectDto dto)
        {
            return projectAppService
                   .Get(new GetProject.GetProjectRequest(new ProjectId(dto.Id)))
                   .Map(IFundedItem (response) =>
                   {
                       var project = Project.Create(response.Project, projectAppService);
                       IFunded funded = project switch
                       {
                           IInvestmentProject investmentProject => new InvestmentFunded(investmentProject, new InvestmentInvestorData(dto, investmentAppService, walletContext)),
                           IFundProject fundProject => new FundFunded(fundProject, new FundInvestorData(dto, investmentAppService, walletContext)),
                           _ => throw new ArgumentOutOfRangeException(nameof(project))
                       };
                       var manage = EnhancedCommand.Create(() => navigator.Go(() => new ManageViewModel(funded, uiServices, investmentAppService, walletContext)));
                   
                       FundedItem fundedItem = new(funded, manage);
                       return fundedItem;
                   });
        }

        private static string GetItemKey(IFundedItem item)
        {
            return item.Funded.Project.Id.Value;
        }

        public void Dispose()
        {
            FindProjects.Dispose();
            Refresh.Dispose();
        }
    }
}

using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Operations;
using Angor.Sdk.Funding.Shared;
using AngorApp.Model.Funded.Fund.Model;
using AngorApp.Model.Funded.Investment.Model;
using AngorApp.Model.Funded.Shared.Model;
using AngorApp.Model.ProjectsV2;
using AngorApp.Model.ProjectsV2.FundProject;
using AngorApp.Model.ProjectsV2.InvestmentProject;
using AngorApp.UI.Sections.Funded.Shared.Manage;
using AngorApp.UI.Shell;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Navigation;
using Zafiro.UI.Shell.Utils;

namespace AngorApp.UI.Sections.Funded.Shared.Section
{
    [Section("Funded", icon: "fa-arrow-trend-up", sortIndex: 3)]
    [SectionGroup("INVESTOR")]
    public class FundedSectionViewModel : IFundedSectionViewModel, IDisposable
    {
        private readonly IInvestmentAppService investmentAppService;
        private readonly IProjectAppService projectAppService;
        private readonly IWalletContext walletContext;
        private readonly INavigator navigator;
        private readonly INotificationService notificationService;
        private readonly ITransactionDraftPreviewer draftPreviewer;

        public FundedSectionViewModel(
            IShellViewModel shell,
            IInvestmentAppService investmentAppService,
            IProjectAppService projectAppService,
            IWalletContext walletContext,
            INavigator navigator,
            INotificationService notificationService,
            ITransactionDraftPreviewer draftPreviewer
        )
        {
            this.investmentAppService = investmentAppService;
            this.projectAppService = projectAppService;
            this.walletContext = walletContext;
            this.navigator = navigator;
            this.notificationService = notificationService;
            this.draftPreviewer = draftPreviewer;
            FindProjects = EnhancedCommand.Create(() => shell.SetSection("Find Projects"));

            RefreshableCollection<IFundedItem, string> fundedProjects = RefreshableCollection.Create(
                GetItems,
                GetItemKey,
                item => item.Funded.InvestorData.InvestedOn == DateTimeOffset.MinValue
                    ? long.MaxValue
                    : -item.Funded.InvestorData.InvestedOn.UtcTicks);
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
                           IInvestmentProject investmentProject => new InvestmentFunded(investmentProject, new InvestmentInvestorData(dto, investmentAppService, walletContext), notificationService, draftPreviewer, investmentAppService, walletContext),
                           IFundProject fundProject => new FundFunded(fundProject, new FundInvestorData(dto, investmentAppService, walletContext), notificationService, draftPreviewer, investmentAppService, walletContext),
                           _ => throw new ArgumentOutOfRangeException(nameof(project))
                       };
                       var manage = EnhancedCommand.Create(() => navigator.Go(() => new ManageViewModel(funded)));

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

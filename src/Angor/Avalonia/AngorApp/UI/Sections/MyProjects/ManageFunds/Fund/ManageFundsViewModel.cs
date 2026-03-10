using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using AngorApp.Model.ProjectsV2.FundProject;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Fund.Claim;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Fund.Release;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Header;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Navigation;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Fund
{
    public class ManageFundsViewModel : IManageFundsViewModel, IHaveHeader
    {
        public ManageFundsViewModel(IFundProject project, IProjectAppService projectAppService, UIServices uiServices, IWalletContext walletContext, IFounderAppService founderAppService)
        {
            Project = project;

            var loadProjectStats = EnhancedCommand.CreateWithResult(async () =>
            {
                project.Refresh.Execute(null);
                return await projectAppService.GetProjectStatistics(project.Id);
            });

            loadProjectStats.HandleErrorsWith(uiServices.NotificationService, "Cannot load project");

            IObservable<ProjectStatisticsDto> projectStatsObs = loadProjectStats.Successes()
                .StartWith(new ProjectStatisticsDto());

            Header = Observable.Return(new HeaderViewModel(project, loadProjectStats));
            ReleaseViewModel = Observable.Return(new ReleaseViewModel(
                project,
                projectStatsObs.Select(stats => stats.AvailableTransactions),
                uiServices,
                founderAppService,
                walletContext));
            ClaimViewModel = Observable.Return(new ClaimViewModel(project, founderAppService, uiServices, walletContext));
            LoadProjectStats = loadProjectStats;
        }
        
        public IObservable<IReleaseViewModel> ReleaseViewModel { get; }
        public IObservable<IClaimViewModel> ClaimViewModel { get; }

        public IFundProject Project { get; }
        public IEnhancedCommand LoadProjectStats { get; }
        public IObservable<object> Header { get; }
    }
}

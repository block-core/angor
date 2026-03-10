using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using AngorApp.Model.ProjectsV2.InvestmentProject;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Claim;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Header;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment.Release;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Navigation;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Investment
{
    public class ManageFundsViewModel : IManageFundsViewModel, IHaveHeader
    {
        public ManageFundsViewModel(IInvestmentProject project, IProjectAppService projectAppService, UIServices uiServices, IWalletContext walletContext, IFounderAppService founderAppService)
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
            var claimViewModel = new ClaimViewModel(project, founderAppService, uiServices, walletContext);
            ClaimViewModel = Observable.Return(claimViewModel);
            LoadProjectStats = loadProjectStats;

            loadProjectStats.Successes().Subscribe(_ => claimViewModel.Load.Execute(null));
        }
        
        public IObservable<IReleaseViewModel> ReleaseViewModel { get; }
        public IObservable<IClaimViewModel> ClaimViewModel { get; }

        public IInvestmentProject Project { get; }
        public IEnhancedCommand LoadProjectStats { get; }
        public IObservable<object> Header { get; }
    }
}

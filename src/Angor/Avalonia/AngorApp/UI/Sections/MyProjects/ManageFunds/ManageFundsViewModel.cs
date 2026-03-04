using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Shared;
using AngorApp.UI.Shared.Samples;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Claim;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Header;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Release;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Navigation;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds
{
    public class ManageFundsViewModel : IManageFundsViewModel, IHaveHeader
    {
        public ManageFundsViewModel(ProjectId projectId, IProjectAppService projectAppService, UIServices uiServices, IWalletContext walletContext, IFounderAppService founderAppService)
        {
            var loadFullProject = EnhancedCommand.CreateWithResult(() => projectAppService.GetFullProject(projectId));
            Load = loadFullProject;
            IObservable<IFullProject> fullProjectObs = loadFullProject.Successes();
            IObservable<IManageFundsProject> projectObs = fullProjectObs.Select(ToProjectView);
            ProjectObs = projectObs;
            ReleaseViewModel = projectObs.Select(project => new ReleaseViewModel(project, uiServices, founderAppService, walletContext));
            ClaimViewModel = projectObs.Select(project => new ClaimViewModel(project, founderAppService, uiServices, walletContext));
            
            loadFullProject.HandleErrorsWith(uiServices.NotificationService, "Cannot load project");
            Header = projectObs.Select(project => new HeaderViewModel(project, loadFullProject));
        }

        public IObservable<IManageFundsProject> ProjectObs { get; }

        public IEnhancedCommand Load { get; }
        public IObservable<IReleaseViewModel> ReleaseViewModel { get; }
        public IObservable<IClaimViewModel> ClaimViewModel { get; }

        public IManageFundsProject Project { get; } = ManageFundsProject.From(new FullProjectSample());
        public IObservable<object> Header { get; }

        private static IManageFundsProject ToProjectView(IFullProject fullProject)
        {
            return ManageFundsProject.From(fullProject);
        }
    }
}

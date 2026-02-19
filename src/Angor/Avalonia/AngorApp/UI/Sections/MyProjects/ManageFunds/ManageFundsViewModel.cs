using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Shared;
using AngorApp.UI.Sections.Browse.Details;
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
            Load = EnhancedCommand.CreateWithResult(() => projectAppService.GetFullProject(projectId));
            ProjectObs = Load.Successes();
            ReleaseViewModel = ProjectObs.Select(project => new ReleaseViewModel(project, uiServices));
            ClaimViewModel = ProjectObs.Select(project => new ClaimViewModel(project, founderAppService, uiServices, walletContext));
            
            Load.HandleErrorsWith(uiServices.NotificationService, "Cannot load project");
            Header = Load.Successes().Select(project => new HeaderViewModel(project, Load));
        }

        public IObservable<IFullProject> ProjectObs { get; }

        public IEnhancedCommand<Result<IFullProject>> Load { get; }
        public IObservable<IReleaseViewModel> ReleaseViewModel { get; }
        public IObservable<IClaimViewModel> ClaimViewModel { get; }

        public IFullProject Project { get; } = new FullProjectSample();
        public IObservable<object> Header { get; }
    }
}
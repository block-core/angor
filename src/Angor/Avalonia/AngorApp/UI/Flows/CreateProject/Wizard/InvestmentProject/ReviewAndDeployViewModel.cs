using System.Reactive.Disposables;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Dtos;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Mappers;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{
    public class ReviewAndDeployViewModel : IHaveTitle, IReviewAndDeployViewModel, IDisposable
    {
        private readonly IProjectDeploymentOrchestrator orchestrator;
        private readonly WalletId walletId;
        private readonly ProjectSeedDto projectSeed;
        private readonly CompositeDisposable disposables = new();

        public IInvestmentProjectConfig NewProject { get; }
        public IEnhancedCommand<Result<string>> DeployCommand { get; }

        public ReviewAndDeployViewModel(
            IInvestmentProjectConfig newProject,
            IProjectDeploymentOrchestrator orchestrator,
            WalletId walletId,
            ProjectSeedDto projectSeed,
            UIServices uiServices)
        {
            NewProject = newProject;
            this.orchestrator = orchestrator;
            this.walletId = walletId;
            this.projectSeed = projectSeed;

            DeployCommand = ReactiveCommand.CreateFromTask(Deploy).Enhance("Deploy").DisposeWith(disposables);
            DeployCommand.HandleErrorsWith(uiServices.NotificationService, "Failed to deploy project").DisposeWith(disposables);
        }

        private async Task<Result<string>> Deploy()
        {
            var dto = NewProject.ToDto();
            return await orchestrator.Deploy(walletId, dto, projectSeed);
        }

        public IObservable<string> Title => Observable.Return("Review & Deploy");

        public void Dispose()
        {
            disposables.Dispose();
        }
    }
}
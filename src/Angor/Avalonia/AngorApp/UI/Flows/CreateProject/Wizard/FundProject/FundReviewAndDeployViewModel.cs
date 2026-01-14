using System.Reactive.Disposables;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Dtos;
using AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Mappers;
using AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject;
using System.Collections.ObjectModel;
using DynamicData;
using AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Helpers;
using System.Linq;

namespace AngorApp.UI.Flows.CreateProject.Wizard.FundProject
{
    public class FundReviewAndDeployViewModel : IFundReviewAndDeployViewModel, IDisposable
    {
        private readonly ProjectDeploymentOrchestrator orchestrator;
        private readonly WalletId walletId;
        private readonly ProjectSeedDto projectSeed;
        private readonly CompositeDisposable disposables = new();

        public IFundProjectConfig NewProject { get; }
        public IEnhancedCommand<Result<string>> DeployCommand { get; }

        public FundReviewAndDeployViewModel(
            IFundProjectConfig newProject,
            ProjectDeploymentOrchestrator orchestrator,
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


            var payoutsSource = new SourceList<IPayoutConfig>();
            payoutsSource.Connect().Bind(out var payouts).Subscribe().DisposeWith(disposables);
            Payouts = payouts;

            if (newProject.PayoutFrequency != null)
            {
                var maxInstallments = newProject.SelectedInstallments.SelectedItems.DefaultIfEmpty(0).Max();
                if (maxInstallments > 0)
                {
                    var generated = PayoutGenerator.Generate(
                        newProject.PayoutFrequency.Value,
                        maxInstallments,
                        DateTime.Now,
                        newProject.MonthlyPayoutDate,
                        newProject.WeeklyPayoutDay
                    );
                    payoutsSource.AddRange(generated);
                }
            }
        }

        public ReadOnlyObservableCollection<IPayoutConfig> Payouts { get; }

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

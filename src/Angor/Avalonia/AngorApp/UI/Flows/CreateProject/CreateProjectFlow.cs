using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects;
using AngorApp.UI.Flows.CreateProject.Wizard;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Stages;
using AngorApp.UI.Flows.CreateProject.Wizard.FundProject;
using AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Model;
using AngorApp.UI.Flows.CreateProject.Wizard.FundProject.Payouts;
using System.Reactive.Threading.Tasks;
using Serilog;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Wizards.Graph.Core;
using Zafiro.UI.Navigation;

namespace AngorApp.UI.Flows.CreateProject
{
    public class CreateProjectFlow(
        INavigator navigator,
        IFounderAppService founderAppService,
        IProjectAppService projectAppService,
        IWalletContext walletContext,
        UIServices uiServices,
        IImagePicker imagePicker,
        ILogger logger
    )
        : ICreateProjectFlow
    {
        public Task<Result<Maybe<string>>> CreateProject()
        {
            return from wallet in walletContext.Require()
                   from seed in GetProjectSeed(wallet.Id)
                   from creationResult in Create(wallet.Id, seed)
                   select creationResult;
        }

        private async Task<Result<Maybe<string>>> Create(WalletId walletId, ProjectSeedDto seed)
        {
            var wizard = CreateProjectWizard(walletId, seed);
            var result = await wizard.Navigate(navigator);

            if (result.HasValue)
            {
                await uiServices.Dialog.ShowMessage(
                    "Success",
                    $"Project {result.Value} created successfully!",
                    "Done",
                    new Icon("fa-check"),
                    DialogTone.Success);
            }

            return result;
        }

        private GraphWizard<string> CreateProjectWizard(WalletId walletId, ProjectSeedDto seed)
        {
            var flow = GraphWizard.For<string>();
            var investmentWizard = CreateInvestmentProjectWizard(walletId, seed);
            var fundWizard = CreateFundProjectWizard(walletId, seed);

            var projectType = new ProjectTypeViewModel();
            var projectTypeNode = flow
                .Step(projectType, projectType.Title)
                .Next(
                    vm => vm.ProjectType.Name switch
                    {
                        "Investment" => investmentWizard,
                        "Fund" => fundWizard,
                        _ => throw new NotImplementedException($"Project type {vm.ProjectType.Name} not implemented"),
                    },
                    canExecute: projectType.WhenAnyValue(x => x.ProjectType).Select(x => x != null))
                .Build();

            var welcome = new WelcomeViewModel();
            var welcomeNode = flow
                .Step(welcome, welcome.Title)
                .Next(_ => projectTypeNode, nextLabel: "Start")
                .Build();

            return new GraphWizard<string>(welcomeNode);
        }

        private IWizardNode<string> CreateInvestmentProjectWizard(WalletId walletId, ProjectSeedDto seed)
        {
            var flow = GraphWizard.For<string>();
            var isDebug = !uiServices.EnableProductionValidations();
            var environment = isDebug ? ValidationEnvironment.Debug : ValidationEnvironment.Production;
            InvestmentProjectConfigBase newProject = isDebug ? new InvestmentProjectConfigDebug() : new InvestmentProjectConfig();

            Action? prefillAction = isDebug ? () => PopulateInvestDebugDefaults(newProject) : null;

            var review = new ReviewAndDeployViewModel(
                newProject,
                new ProjectDeploymentOrchestrator(
                    projectAppService,
                    founderAppService,
                    uiServices,
                    logger),
                walletId,
                seed,
                uiServices);

            var reviewNode = flow
                .Step(review, review.Title)
                .Finish(vm => vm.DeployCommand.Execute().ToTask(), review.DeployCommand.CanExecute, "Deploy")
                .Build();

            var stages = new StagesViewModel(newProject);
            var stagesNode = flow
                .Step(stages, stages.Title)
                .Next(_ => reviewNode, stages.IsValid)
                .Build();

            var funding = new FundingConfigurationViewModel(newProject, environment);
            var fundingNode = flow
                .Step(funding, funding.Title)
                .Next(_ => stagesNode, funding.IsValid)
                .Build();

            var images = new ProjectImagesViewModel(newProject, imagePicker);
            var imagesNode = flow
                .Step(images, images.Title)
                .Next(_ => fundingNode)
                .Build();

            var profile = new ProjectProfileViewModel(newProject, prefillAction);
            return flow
                .Step(profile, profile.Title)
                .Next(_ => imagesNode, profile.IsValid)
                .Build();
        }

        private IWizardNode<string> CreateFundProjectWizard(WalletId walletId, ProjectSeedDto seed)
        {
            var flow = GraphWizard.For<string>();
            var isDebug = !uiServices.EnableProductionValidations();
            var newProject = new FundProjectConfig();

            Action? prefillAction = isDebug ? () => PopulateFundDebugDefaults(newProject) : null;

            var review = new FundReviewAndDeployViewModel(
                newProject,
                new ProjectDeploymentOrchestrator(
                    projectAppService,
                    founderAppService,
                    uiServices,
                    logger),
                walletId,
                seed,
                uiServices);

            var reviewNode = flow
                .Step(review, review.Title)
                .Finish(vm => vm.DeployCommand.Execute().ToTask(), review.DeployCommand.CanExecute, "Deploy")
                .Build();

            var payouts = new FundPayoutsViewModel(newProject);
            var payoutsNode = flow
                .Step(payouts, payouts.Title)
                .Next(_ => reviewNode, payouts.IsValid)
                .Build();

            var goal = new GoalViewModel(newProject);
            var goalNode = flow
                .Step(goal, goal.Title)
                .Next(_ => payoutsNode, goal.IsValid)
                .Build();

            var images = new ProjectImagesViewModel(newProject, imagePicker);
            var imagesNode = flow
                .Step(images, images.Title)
                .Next(_ => goalNode)
                .Build();

            var profile = new ProjectProfileViewModel(newProject, prefillAction);
            return flow
                .Step(profile, profile.Title)
                .Next(_ => imagesNode, profile.IsValid)
                .Build();
        }

        private static void PopulateFundDebugDefaults(FundProjectConfig project)
        {
            var id = Guid.NewGuid().ToString()[..8];
            project.Name = $"Debug Fund {id}";
            project.Description = $"Auto-populated debug fund {id} for testing on testnet. Created at {DateTime.Now:HH:mm:ss}.";
            project.Website = "https://angor.io";

            project.GoalAmount = AmountUI.FromBtc(0.5m);
            project.Threshold = AmountUI.FromBtc(0.01m);
            project.PenaltyDays = 0;

            project.PayoutFrequency = PayoutFrequency.Monthly;
            project.MonthlyPayoutDate = DateTime.Now.Day;

            // Pick reasonable default installment patterns (3-month and 6-month).
            project.SelectedInstallments.SelectionModel.Clear();
            project.SelectedInstallments.SelectionModel.Select(0);
            project.SelectedInstallments.SelectionModel.Select(1);
        }

        private static void PopulateInvestDebugDefaults(InvestmentProjectConfigBase project)
        {
            var id = Guid.NewGuid().ToString()[..8];
            project.Name = $"Debug Project {id}";
            project.Description = $"Auto-populated debug project {id} for testing on testnet. Created at {DateTime.Now:HH:mm:ss}.";
            project.Website = "https://angor.io";
            project.TargetAmount = AmountUI.FromBtc(0.01);
            project.PenaltyDays = 0;
            project.StartDate = DateTime.Now.Date;
            project.FundingEndDate = DateTime.Now.Date;
            project.ExpiryDate = DateTime.Now.Date.AddDays(31);

            // Add stages with dates matching the funding end date for immediate release
            project.CreateAndAddStage(0.10m, DateTime.Now.Date);
            project.CreateAndAddStage(0.30m, DateTime.Now.Date);
            project.CreateAndAddStage(0.60m, DateTime.Now.Date);
        }

        private async Task<Result<ProjectSeedDto>> GetProjectSeed(WalletId walletId)
        {
            var result = await founderAppService.CreateProjectKeys(new CreateProjectKeys.CreateProjectKeysRequest(walletId));
            return result.Map(response => response.ProjectSeedDto);
        }
    }
}

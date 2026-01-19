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
using AngorApp.UI.Shared.Controls.Common.Success;
using Serilog;
using Zafiro.Avalonia.Controls.Wizards.Slim;
using Zafiro.UI.Navigation;
using Zafiro.UI.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;
using Avalonia.Threading;

namespace AngorApp.UI.Flows.CreateProject
{
    public class CreateProjectFlowV2(
        INavigator navigator,
        IFounderAppService founderAppService,
        IProjectAppService projectAppService,
        IWalletContext walletContext,
        UIServices uiServices,
        ILogger logger
    )
        : ICreateProjectFlow
    {
        public Task<Result<Maybe<string>>> CreateProject()
        {
            return from wallet in walletContext.GetDefaultWallet()
                   from seed in GetProjectSeed(wallet.Id)
                   from creationResult in Create(wallet.Id, seed)
                   select creationResult;
        }

        private async Task<Result<Maybe<string>>> Create(WalletId walletId, ProjectSeedDto seed)
        {
            SlimWizard<string> rootWizard = WizardBuilder
                                            .StartWith(() => new WelcomeViewModel()).NextCommand(model => model.Start)
                                            .Then(_ => new ProjectTypeViewModel())
                                            .NextCommand<Unit, ProjectTypeViewModel, string>(vm => CreateProjectOftype(
                                                vm,
                                                walletId,
                                                seed))
                                            .Then(txId => new SuccessViewModel($"Project {txId} created successfully!"), "Success").Next((_, s) => s, "Finish").Always()
                                            .WithCompletionFinalStep();

            return await rootWizard.Navigate(navigator);
        }

        private IEnhancedCommand<Result<string>> CreateProjectOftype(
            ProjectTypeViewModel vm,
            WalletId walletId,
            ProjectSeedDto seed
        )
        {
            var canExecute = vm.WhenAnyValue(x => x.ProjectType).Select(x => x != null);

            return ReactiveCommand.CreateFromTask(async () =>
            {
                var projectType = vm.ProjectType;
                return await Dispatcher.UIThread.InvokeAsync(() => projectType.Name switch
                {
                    "Investment" => CreateInvestmentProjectWizard(walletId, seed).Navigate(navigator).ToResult("Wizard was cancelled by user"),
                    "Fund" => CreateFundProjectWizard(walletId, seed).Navigate(navigator).ToResult("Wizard was cancelled by user"),
                    _ => throw new NotImplementedException($"Project type {projectType.Name} not implemented")
                });
            }, canExecute).Enhance();
        }

        private SlimWizard<string> CreateInvestmentProjectWizard(WalletId walletId, ProjectSeedDto seed)
        {
            InvestmentProjectConfigBase newProject =
                uiServices.EnableProductionValidations() ? new InvestmentProjectConfig() : new InvestmentProjectConfigDebug();

            SlimWizard<string> wizard = WizardBuilder
                                        .StartWith(() => new ProjectProfileViewModel(newProject)).NextUnit().WhenValid()
                                        .Then(_ => new ProjectImagesViewModel(newProject, new ImagePicker(uiServices))).NextUnit().Always()
                                        .Then(_ => new FundingConfigurationViewModel(newProject)).NextUnit().WhenValid()
                                        .Then(_ => new StagesViewModel(newProject)).NextUnit().WhenValid()
                                        .Then(_ => new ReviewAndDeployViewModel(
                                                  newProject,
                                                  new ProjectDeploymentOrchestrator(
                                                      projectAppService,
                                                      founderAppService,
                                                      uiServices,
                                                      logger),
                                                  walletId,
                                                  seed,
                                                  uiServices))
                                        .NextCommand(review => review.DeployCommand)
                                        .WithCommitFinalStep();

            return wizard;
        }

        private SlimWizard<string> CreateFundProjectWizard(WalletId walletId, ProjectSeedDto seed)
        {
            var newProject = new FundProjectConfig();

            SlimWizard<string> wizard = WizardBuilder
                                        .StartWith(() => new ProjectProfileViewModel(newProject)).NextUnit().WhenValid()
                                        .Then(_ => new ProjectImagesViewModel(newProject, new ImagePicker(uiServices))).NextUnit().Always()
                                        .Then(_ => new GoalViewModel(newProject)).NextUnit().WhenValid()
                                        .Then(_ => new FundPayoutsViewModel(newProject)).NextUnit().WhenValid()
                                        .Then(_ => new FundReviewAndDeployViewModel(
                                                  newProject,
                                                  new ProjectDeploymentOrchestrator(
                                                      projectAppService,
                                                      founderAppService,
                                                      uiServices,
                                                      logger),
                                                  walletId,
                                                  seed,
                                                  uiServices))
                                        .NextCommand(review => review.DeployCommand)
                                        .WithCommitFinalStep();

            return wizard;
        }

        private async Task<Result<ProjectSeedDto>> GetProjectSeed(WalletId walletId)
        {
            var result = await founderAppService.CreateProjectKeys(new CreateProjectKeys.CreateProjectKeysRequest(walletId));
            return result.Map(response => response.ProjectSeedDto);
        }
    }
}

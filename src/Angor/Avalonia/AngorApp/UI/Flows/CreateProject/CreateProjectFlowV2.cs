using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects;
using AngorApp.UI.Flows.CreateProject.Wizard;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Stages;
using AngorApp.UI.Shared.Controls.Common.Success;
using Serilog;
using Zafiro.Avalonia.Controls.Wizards.Slim;
using Zafiro.UI.Navigation;
using Zafiro.UI.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;
using ProjectType = AngorApp.UI.Flows.CreateProject.Wizard.ProjectType;

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
                                                vm.ProjectType,
                                                walletId,
                                                seed))
                                            .Then(txId => new SuccessViewModel($"Project {txId} created successfully!"), "Success").Next((_, s) => s, "Finish").Always()
                                            .WithCompletionFinalStep();

            return await rootWizard.Navigate(navigator);
        }

        private IEnhancedCommand<Result<string>> CreateProjectOftype(
            ProjectType projectType,
            WalletId walletId,
            ProjectSeedDto seed
        )
        {
            // TODO: We support only investment projects for now. That's why we ignore projectType.
            return CreateInvestmentProject(walletId, seed);
        }

        private IEnhancedCommand<Result<string>> CreateInvestmentProject(WalletId walletId, ProjectSeedDto seed)
        {
            return ReactiveCommand
                   .CreateFromTask(() => CreateInvestmentProjectWizard(walletId, seed).Navigate(navigator).ToResult("Wizard was cancelled by user"))
                   .Enhance();
        }

        private SlimWizard<string> CreateInvestmentProjectWizard(WalletId walletId, ProjectSeedDto seed)
        {
            InvestmentProjectConfigBase newProject =
                uiServices.EnableProductionValidations() ? new InvestmentProjectConfig() : new InvestmentProjectConfigDebug();

            SlimWizard<string> wizard = WizardBuilder
                                        .StartWith(() => new ProjectProfileViewModel(newProject)).NextUnit().WhenValid()
                                        .Then(_ => new ProjectImagesViewModel(newProject)).NextUnit().Always()
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

        private async Task<Result<ProjectSeedDto>> GetProjectSeed(WalletId walletId)
        {
            var result = await founderAppService.CreateProjectKeys(new CreateProjectKeys.CreateProjectKeysRequest(walletId));
            return result.Map(response => response.ProjectSeedDto);
        }
    }
}

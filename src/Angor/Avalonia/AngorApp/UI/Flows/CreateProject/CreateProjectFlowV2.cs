using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using AngorApp.UI.Flows.CreateProject.Wizard;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;
using Zafiro.Avalonia.Controls.Wizards.Slim;
using Zafiro.UI.Navigation;
using Zafiro.UI.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;

namespace AngorApp.UI.Flows.CreateProject
{
    public class CreateProjectFlowV2(
        INavigator navigator,
        IFounderAppService founderAppService,
        IWalletContext walletContext
    )
        : ICreateProjectFlow
    {
        public Task<Result<Maybe<string>>> CreateProject()
        {
            var createWizardResult = from wallet in walletContext.GetDefaultWallet()
                from seed in GetProjectSeed(wallet.Id)
                select CreateInvestmentProjectWizard();

            NewProject newProject = null!;

            var rootWizard = WizardBuilder
                             .StartWith(() => new WelcomeViewModel()).NextCommand(model => model.Start)
                             .Then(_ => new ProjectTypeViewModel())
                             .NextCommand<Unit, ProjectTypeViewModel, String>(_ => StartSubwizard())
                             .WithCompletionFinalStep();

            return createWizardResult.Map(wizard => rootWizard.Navigate(navigator));
        }

        private IEnhancedCommand<Result<string>> StartSubwizard()
        {
            return ReactiveCommand
                   .CreateFromTask(() => CreateInvestmentProjectWizard().Navigate(navigator).ToResult("Salute"))
                   .Enhance();
        }

        private SlimWizard<string> CreateInvestmentProjectWizard()
        {
            var newProject = new NewProject();
            var wizard = WizardBuilder
                         .StartWith(() => new ProjectProfileViewModel(newProject)).NextUnit().Always()
                         .Then(_ => new ProjectImagesViewModel(newProject)).NextUnit().Always()
                         .Then(_ => new FundingConfigurationViewModel(newProject)).NextUnit().Always()
                         .Then(_ => new StagesViewModel(newProject)).NextUnit().Always()
                         .Then(_ => new ReviewAndDeployViewModel(newProject)).NextResult(_ => Result.Success(""))
                         .Always()
                         .WithCommitFinalStep();

            return wizard;
        }

        private async Task<Result<ProjectSeedDto>> GetProjectSeed(WalletId walletId)
        {
            var result = await founderAppService.CreateNewProjectKeysAsync(new CreateProjectNewKeys.CreateProjectNewKeysRequest(walletId));
            return result.Map(response => response.ProjectSeedDto);
        }
    }
}
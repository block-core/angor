using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using AngorApp.Core;
using AngorApp.Sections.Founder.CreateProject;
using AngorApp.Sections.Founder.CreateProject.ProjectCreated;
using Zafiro.Avalonia.Controls.Wizards.Slim;
using Zafiro.UI.Navigation;
using Zafiro.UI.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;

namespace AngorApp.Flows.CreateProyect;

public class CreateProjectFlow(UIServices uiServices, INavigator navigator, IProjectAppService projectAppService, SharedCommands commands, IWalletContext walletContext, INetworkConfiguration networkConfiguration)
    : ICreateProjectFlow
{
    public Task<Result<Maybe<string>>> CreateProject()
    {
   var createWizardResult = 
    from wallet in walletContext.CurrentWallet.ToResult("A wallet is required to create a project")
       from projectSeed in GetProjectSeed()
      select CreateWizard(wallet, projectSeed);

        return createWizardResult.Map(slimWizard => slimWizard.Navigate(navigator));
    }

    private SlimWizard<string> CreateWizard(IWallet wallet, ProjectSeed projectSeed)
    {
      var wizard = WizardBuilder
       .StartWith(() => new CreateProjectViewModel(wallet, projectSeed, uiServices, projectAppService, networkConfiguration), "Create Project").NextCommand(model => model.Create)
       .Then(transactionId => new ProjectCreatedViewModel(transactionId, commands), "Success").Next((_, projectId) => projectId, "Close").Always()
    .WithCompletionFinalStep();

      return wizard;
    }

    private static async Task<Result<ProjectSeed>> GetProjectSeed()
    {
        // TODO: Implement real project seed generation logic
   return Result.Success(new ProjectSeed("123456789abcdef123456789abcdef123456789abcdef123456789abcdef"));
    }

    public record ProjectSeed(string NostrPubKey);
}
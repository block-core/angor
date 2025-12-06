using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using AngorApp.Core;
using AngorApp.UI.Sections.Founder.CreateProject;
using AngorApp.UI.Sections.Founder.CreateProject.ProjectCreated;
using Microsoft.Extensions.Logging;
using Zafiro.Avalonia.Controls.Wizards.Slim;
using Zafiro.UI.Navigation;
using Zafiro.UI.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;

namespace AngorApp.UI.Flows.CreateProject;

public class CreateProjectFlow(
    UIServices uiServices, 
    INavigator navigator, 
    IProjectAppService projectAppService,
    IFounderAppService founderAppService,
    SharedCommands commands, 
    IWalletContext walletContext, 
    ILogger<CreateProjectViewModel> logger)
    : ICreateProjectFlow
{
    public Task<Result<Maybe<string>>> CreateProject()
    {
        var createWizardResult = from wallet in walletContext.GetDefaultWallet()
            from seed in GetProjectSeed()
            select CreateWizard(wallet, seed);

        return createWizardResult.Map(slimWizard => slimWizard.Navigate(navigator));
    }

    private SlimWizard<string> CreateWizard(IWallet wallet, ProjectSeed projectSeed)
    {
        var wizard = WizardBuilder
            .StartWith(() => new CreateProjectViewModel(wallet, projectSeed, uiServices, projectAppService, founderAppService, logger), "Create Project").NextCommand(model => model.Create)
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
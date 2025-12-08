using Angor.Contexts.CrossCutting;
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

public class CreateProjectFlow(UIServices uiServices, INavigator navigator, IProjectAppService projectAppService, SharedCommands commands, IWalletContext walletContext, ILogger<CreateProjectViewModel> logger,
    IFounderAppService founderAppService)
    : ICreateProjectFlow
{
    public Task<Result<Maybe<string>>> CreateProject()
    {
        var createWizardResult =
            from wallet in walletContext.GetDefaultWallet()
            from projectSeed in GetProjectSeed(wallet.Id)
            select CreateWizard(wallet, projectSeed);

        return createWizardResult.Map(slimWizard => slimWizard.Navigate(navigator));
    }

    private SlimWizard<string> CreateWizard(IWallet wallet, ProjectSeed projectSeed)
    {
        var wizard = WizardBuilder
            .StartWith(() => new CreateProjectViewModel(wallet, projectSeed, uiServices, projectAppService, logger), "Create Project").NextCommand(model => model.Create)
            .Then(transactionId => new ProjectCreatedViewModel(transactionId, commands), "Success").Next((_, projectId) => projectId, "Close").Always()
            .WithCompletionFinalStep();

        return wizard;
    }

    private Task<Result<ProjectSeed>> GetProjectSeed(WalletId walletId)
    {
         return founderAppService.StartNewProjectAsync(walletId)
             .Map(seed => new ProjectSeed(seed.FounderKey, seed.FounderRecoveryKey, seed.NostrPubKey, seed.ProjectIdentifier));
    }

    public record ProjectSeed(string FounderKey, string FounderRecoveryKey, string NostrPubKey, string ProjectIdentifier);
}
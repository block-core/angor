using System.Threading.Tasks;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using AngorApp.Sections.Founder.CreateProject;
using AngorApp.Sections.Founder.CreateProject.ProjectCreated;
using AngorApp.UI.Services;
using Zafiro.Avalonia.Controls.Wizards.Slim;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;
using Zafiro.UI.Wizards.Slim;
using Zafiro.UI.Wizards.Slim.Builder;

namespace AngorApp.Flows;

public class CreateProjectFlow : ICreateProjectFlow
{
    private readonly UIServices uiServices;
    private readonly INavigator navigator;
    private readonly IProjectAppService projectAppService;
    private readonly INetworkStorage networkConfiguration;

    public CreateProjectFlow(UIServices uiServices, INavigator navigator, IProjectAppService projectAppService, INetworkStorage networkConfiguration)
    {
        this.uiServices = uiServices;
        this.navigator = navigator;
        this.projectAppService = projectAppService;
        this.networkConfiguration = networkConfiguration;
    }
    
    public Task<Result<Maybe<string>>> CreateProject()
    {
        var createWizardResult = 
            from wallet in uiServices.WalletRoot.TryDefaultWalletAndActivate()
            from projectSeed in GetProjectSeed()
            select CreateWizard(wallet, projectSeed);

        return createWizardResult.Map(slimWizard => slimWizard.Navigate(navigator));
    }

    private SlimWizard<string> CreateWizard(IWallet wallet, ProjectSeed projectSeed)
    {
        var wizard = WizardBuilder
            .StartWith(() => new CreateProjectViewModel(wallet,  projectSeed, uiServices, projectAppService), "Create Project").ProceedWith(model => model.Create)
            .Then(transactionId => new ProjectCreatedViewModel(transactionId, uiServices, networkConfiguration), "Success").ProceedWith((_, projectId) => ReactiveCommand.Create(() => Result.Success(projectId)).Enhance("Close"))
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
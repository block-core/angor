using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using AngorApp.UI.Services;
using ProjectId = Angor.Contexts.Funding.Shared.ProjectId;

namespace AngorApp.Sections.Founder.ProjectDetails;

public class FounderProjectDetailsViewModelFactory : IFounderProjectDetailsViewModelFactory
{
    private readonly IProjectAppService projectAppService;
    private readonly IFounderAppService founderAppService;
    private readonly UIServices uiServices;
    private readonly IWalletContext walletContext;

    public FounderProjectDetailsViewModelFactory(
        IProjectAppService projectAppService,
        IFounderAppService founderAppService,
        UIServices uiServices,
        IWalletContext walletContext)
    {
        this.projectAppService = projectAppService;
        this.founderAppService = founderAppService;
        this.uiServices = uiServices;
        this.walletContext = walletContext;
    }

    public FounderProjectDetailsViewModel Create(ProjectId projectId)
    {
        return new FounderProjectDetailsViewModel(projectId, projectAppService, founderAppService, uiServices, walletContext);
    }
}

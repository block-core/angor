using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using ProjectId = Angor.Contexts.Funding.Shared.ProjectId;

namespace AngorApp.Sections.Founder.ProjectDetails;

public class FounderProjectDetailsViewModelFactory(
    IProjectAppService projectAppService,
    IFounderAppService founderAppService,
    UIServices uiServices,
    IWalletContext walletContext)
    : IFounderProjectDetailsViewModelFactory
{
    public FounderProjectDetailsViewModel Create(ProjectId projectId)
    {
        return new FounderProjectDetailsViewModel(projectId, projectAppService, founderAppService, uiServices, walletContext);
    }
}

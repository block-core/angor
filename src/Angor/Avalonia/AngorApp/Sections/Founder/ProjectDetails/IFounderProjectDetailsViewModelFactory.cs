using ProjectId = Angor.Contexts.Funding.Shared.ProjectId;

namespace AngorApp.Sections.Founder.ProjectDetails;

public interface IFounderProjectDetailsViewModelFactory
{
    FounderProjectDetailsViewModel Create(ProjectId projectId);
}

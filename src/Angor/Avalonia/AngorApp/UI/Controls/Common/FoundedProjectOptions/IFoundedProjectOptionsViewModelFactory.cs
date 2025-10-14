using Angor.Contexts.Funding.Shared;

namespace AngorApp.UI.Controls.Common.FoundedProjectOptions;

public interface IFoundedProjectOptionsViewModelFactory
{
    IFoundedProjectOptionsViewModel Create(ProjectId projectId);
}

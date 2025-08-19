using AngorApp.Sections.Founder.ProjectDetails.MainView;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails;

public interface IFounderProjectDetailsViewModel
{
    public IEnhancedCommand<Result<IProjectMainViewModel>> Load { get; }
    public IProjectMainViewModel ProjectMain { get; }
}
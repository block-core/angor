using Zafiro.Commands;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject.ProjectCreated;

public interface IProjectCreatedViewModel
{
    public IEnhancedCommand<Result> OpenTransaction { get; }
}
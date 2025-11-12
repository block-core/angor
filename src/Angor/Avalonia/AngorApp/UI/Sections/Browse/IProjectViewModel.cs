using System.Windows.Input;

namespace AngorApp.UI.Sections.Browse;

public interface IProjectViewModel
{
    IProject Project { get; }
    IEnhancedCommand<Result> GoToDetails { get; }
}

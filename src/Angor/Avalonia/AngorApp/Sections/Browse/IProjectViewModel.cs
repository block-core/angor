using System.Windows.Input;
using Angor.UI.Model;

namespace AngorApp.Sections.Browse;

public interface IProjectViewModel
{
    IProject Project { get; }
    IEnhancedCommand<Result> GoToDetails { get; }
}

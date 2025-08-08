using System.Windows.Input;
using Angor.UI.Model;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Browse;

public interface IProjectViewModel
{
    IProject Project { get; }
    public IEnhancedCommand GoToDetails { get; set; }
}
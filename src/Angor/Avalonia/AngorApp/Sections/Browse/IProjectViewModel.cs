using System.Windows.Input;
using AngorApp.Model;

namespace AngorApp.Sections.Browse;

public interface IProjectViewModel
{
    IProject Project { get; }
    public ICommand GoToDetails { get; set; }
}
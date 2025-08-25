using System.Windows.Input;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Home;

public interface IHomeSectionViewModel
{
    public ICommand OpenHub { get; }
    public IEnhancedCommand GoToAngorFlow { get; set; }
    public ReactiveCommand<string, Unit> GoToSection { get; }
}
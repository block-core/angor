using Zafiro.UI.Navigation.Sections;
using Zafiro.UI.Shell;

namespace AngorApp.Sections.Shell;

public class MainViewModelDesign : IMainViewModel
{
    public ReactiveCommand<Unit, Unit> OpenHub { get; }
    public IShell Shell { get; } = new ShellDesign();
    public IEnumerable<ISection> Sections { get; }
    public IContentSection SelectedSection { get; set; }

    public void GoToSection(string sectionName)
    {
    }
}
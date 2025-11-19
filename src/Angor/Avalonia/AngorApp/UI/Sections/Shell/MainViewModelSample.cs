using Zafiro.UI.Navigation.Sections;
using Zafiro.UI.Shell;

namespace AngorApp.UI.Sections.Shell;

public class MainViewModelSample : IMainViewModel
{
    public ReactiveCommand<Unit, Unit> OpenHub { get; }
    public IShell Shell { get; } = new ShellSample();

    public ISection SelectedSection { get; set; }

    public void GoToSection(string sectionName)
    {
    }
}

using Zafiro.UI.Navigation.Sections;
using Zafiro.UI.Shell;

namespace AngorApp.Sections.Shell;

public class MainViewModelSample : IMainViewModel
{
    public ReactiveCommand<Unit, Unit> OpenHub { get; }
    public IShell Shell { get; } = new ShellSample();

    public IContentSection SelectedSection { get; set; }

    public void GoToSection(string sectionName)
    {
    }
}
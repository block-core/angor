using System.Linq;
using AngorApp.Core;
using AngorApp.Services;
using ReactiveUI.SourceGenerators;

namespace AngorApp.Sections.Shell;

public partial class MainViewModel : ReactiveObject, IMainViewModel
{
    [Reactive] private Section selectedSection;

    public MainViewModel(IEnumerable<SectionBase> sections, UIServices uiServices)
    {
        Sections = sections;
        SelectedSection = Sections.OfType<Section>().Skip(0).First();
        OpenHub = ReactiveCommand.CreateFromTask(() => uiServices.LauncherService.LaunchUri(Constants.AngorHubUri));
    }

    public ReactiveCommand<Unit,Unit> OpenHub { get; }

    public IEnumerable<SectionBase> Sections { get; }

    public void GoToSection(string sectionName)
    {
        SelectedSection = Sections.OfType<Section>().First(x => x.Name == sectionName);
    }
}
using System.Linq;
using AngorApp.Services;
using ReactiveUI.SourceGenerators;

namespace AngorApp.Sections.Shell;

public partial class MainViewModel : ReactiveObject, IMainViewModel
{
    [Reactive] private Section selectedSection;

    public MainViewModel(IEnumerable<SectionBase> sections, UIServices uiServices)
    {
        Sections = sections;
        SelectedSection = Sections.OfType<Section>().Skip(1).First();
        OpenHub = ReactiveCommand.CreateFromTask(() => uiServices.LauncherService.Launch(new Uri("https://www.angor.io")));
    }

    public ReactiveCommand<Unit,Unit> OpenHub { get; set; }

    public IEnumerable<SectionBase> Sections { get; }
}
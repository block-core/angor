using System.Linq;
using System.Reactive.Linq;
using AngorApp.Core;
using AngorApp.Sections.Shell.Sections;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;

namespace AngorApp.Sections.Shell;

public partial class MainViewModel : ReactiveObject, IMainViewModel
{
    [Reactive] private IContentSection selectedSection;

    public MainViewModel(IEnumerable<SectionBase> sections, UIServices uiServices)
    {
        Sections = sections;
        SelectedSection = Sections.OfType<IContentSection>().First();
        OpenHub = ReactiveCommand.CreateFromTask(() => uiServices.LauncherService.LaunchUri(Constants.AngorHubUri));
        CurrentContent = this.WhenAnyValue(x => x.SelectedSection).Select(section => section.GetViewModel());
    }

    public ReactiveCommand<Unit, Unit> OpenHub { get; }

    public IEnumerable<SectionBase> Sections { get; }
    public IObservable<object?> CurrentContent { get; }

    public void GoToSection(string sectionName)
    {
        SelectedSection = Sections.OfType<IContentSection>().First(x => x.Name == sectionName);
    }
}
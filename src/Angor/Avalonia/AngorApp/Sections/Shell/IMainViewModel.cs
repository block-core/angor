namespace AngorApp.Sections.Shell;

public interface IMainViewModel
{
    ReactiveCommand<Unit, Unit> OpenHub { get; }
    IEnumerable<SectionBase> Sections { get; }
    IContentSection SelectedSection { get; set; }
    void GoToSection(string sectionName);
}
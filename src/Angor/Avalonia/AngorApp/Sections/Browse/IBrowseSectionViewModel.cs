namespace AngorApp.Sections.Browse;

public interface IBrowseSectionViewModel
{
    public IReadOnlyCollection<ProjectViewModel> Projects { get; set; }
    ReactiveCommand<Unit, Unit> OpenHub { get; set; }
}
namespace AngorApp.Sections.Browse;

public interface IBrowseViewModel
{
    public IReadOnlyCollection<ProjectViewModel> Projects { get; set; }
    ReactiveCommand<Unit, Unit> OpenHub { get; set; }
}
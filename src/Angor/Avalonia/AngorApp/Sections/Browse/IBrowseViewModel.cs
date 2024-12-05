namespace AngorApp.Sections.Browse;

public interface IBrowseViewModel
{
    public IReadOnlyCollection<Project> Projects { get; set; }
    ReactiveCommand<Unit, Unit> OpenHub { get; set; }
}
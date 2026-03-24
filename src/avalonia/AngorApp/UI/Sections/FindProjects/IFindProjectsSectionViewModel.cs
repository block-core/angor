namespace AngorApp.UI.Sections.FindProjects
{
    public interface IFindProjectsSectionViewModel
    {
        IEnumerable<IFindProjectItem> Projects { get; }
        IEnhancedCommand<Result<IEnumerable<FindProjectItem>>> LoadProjects { get; }
    }
}
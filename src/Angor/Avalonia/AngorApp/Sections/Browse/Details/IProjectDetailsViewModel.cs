namespace AngorApp.Sections.Browse.Details;

public interface IProjectDetailsViewModel : IProject
{
    string Name { get; }
    string ShortDescription { get; }
    object Icon { get; }
    object Picture { get; }
}
namespace AngorApp.UI.Sections.Funders.Grouping;

using AngorApp.UI.Sections.Funders.Items;
public interface IFunderGroup
{
    public string Name { get; }
    public IReadOnlyCollection<IFunderItem> Funders { get; }
}
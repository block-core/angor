namespace AngorApp.Sections.Shell;

public class Section(string name, Func<object> getViewModel, object? icon = null) : SectionBase
{
    public string Name { get; } = name;
    public Func<object> GetViewModel { get; } = getViewModel;
    public object? Icon { get; } = icon;
}
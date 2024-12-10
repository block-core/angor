namespace AngorApp.Sections.Shell;

public class Section(string name, object viewModel, object? icon = null) : SectionBase
{
    public string Name { get; } = name;
    public object ViewModel { get; } = viewModel;
    public object? Icon { get; } = icon;
}
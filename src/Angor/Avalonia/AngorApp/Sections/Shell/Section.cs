using Zafiro.UI;

namespace AngorApp.Sections.Shell;

public class Section<T>(string name, Func<T> getViewModel, object? icon = null) : SectionBase, IContentSection
{
    public string Name { get; } = name;

    Func<object?> IContentSection.GetViewModel => () => GetViewModel();
    public Func<T> GetViewModel { get; } = getViewModel;

    public object? Icon { get; } = icon;
}

public class Section
{
    public static Section<T> Create<T>(string name, Func<T> getViewModel, object? icon = null)
    {
        return new Section<T>(name, getViewModel, icon);
    }
}

public interface IContentSection
{
    public string Name { get; }
    public Func<object?> GetViewModel { get; }
    public object? Icon { get; }
}
using System.ComponentModel;
using Zafiro.Reactive;
using Zafiro.UI.Navigation.Sections;

namespace AngorApp.Composition.Registrations.Sections;

public class LazySection<T> : ISection where T : notnull
{
    public LazySection(string name, SectionGroup group, object? icon, Func<T> createContent)
    {
        Name = name;
        Group = group;
        Icon = icon;
        FriendlyName = Name;
        Content = Observable.Defer(() => Observable.Return((object)createContent())).ReplayLastActive();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public bool IsVisible { get; set; } = true;
    public int SortOrder { get; set; }
    public string Name { get; }
    public string FriendlyName { get; }
    public SectionGroup Group { get; }
    public object? Icon { get; }
    public IObservable<object> Content { get; }
}
using ReactiveUI.SourceGenerators;
using Zafiro.Avalonia.Controls.Shell;
using Zafiro.UI.Navigation.Sections;

namespace AngorApp.Composition.Registrations.Sections;

public partial class DynamicContentSection : ReactiveObject, IContentSection
{
    private readonly IContentSection section;
    
    [Reactive]
    private bool isVisible = true;
    [Reactive]
    private int sortOrder;

    public DynamicContentSection(IContentSection section)
    {
        this.section = section;
        MessageBus.Current.ListenIncludeLatest<ShellView>()
            .Select(view => Observable.FromEventPattern<EventHandler, EventArgs>(h => view.LayoutUpdated += h, h => view.LayoutUpdated -= h))
            .Switch()
            .Throttle(TimeSpan.FromSeconds(0.5), RxApp.MainThreadScheduler)
            .Subscribe(pattern =>
            {
                AdaptToShell((ShellView)pattern.Sender);
            });
    }

    private void AdaptToShell(ShellView shell)
    {
        if (shell.Bounds.Size.Width > Breakpoint)
        {
            IsVisible = WideVisibility; 
            SortOrder = WideSortOrder;
        }
        else
        {
            SortOrder = NarrowSortOrder;
            IsVisible = NarrowVisibility;
        }
    }

    public double Breakpoint { get; set; } = 500;

    public int NarrowSortOrder { get; set; }
    public int WideSortOrder { get; set; }

    public bool NarrowVisibility { get; set; } = true;
    public bool WideVisibility { get; set; } = true;
    
    public bool IsPrimary
    {
        get => section.IsPrimary;
        set => section.IsPrimary = value;
    }

    public string Name => section.Name;
    public string FriendlyName => section.FriendlyName;

    public object? Icon => section.Icon;

    public IObservable<object> Content => section.Content;

    public Type RootType => section.RootType;
}
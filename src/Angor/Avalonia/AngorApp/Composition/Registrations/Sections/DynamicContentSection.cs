using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Zafiro.Avalonia.Controls.Shell;
using Zafiro.UI.Navigation.Sections;

namespace AngorApp.Composition.Registrations.Sections;

public partial class DynamicContentSection<T> : ContentSection<T> where T : class
{

    public DynamicContentSection(ContentSection<T> section) : base(section.Name, section.Content.Select(o => (T)o), section.Icon, section.Initialize)
    {
        IsVisible = section.IsVisible;
        SortOrder = section.SortOrder;
        
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
}

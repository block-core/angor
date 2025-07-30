using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls.Platform;
using Avalonia.Data;
using Avalonia.Xaml.Interactions.Custom;
using Zafiro.Avalonia;

namespace AngorApp;

public class SafeAreaBehavior : AttachedToLogicalTreeBehavior<Visual>
{
    public SafeAreaBehavior()
    {
    }
    
    protected override IDisposable OnAttachedToLogicalTreeOverride()
    {
        var topLevel = TopLevel.GetTopLevel(AssociatedObject);
        if (topLevel != null)
        {
            return Observable.FromEventPattern<SafeAreaChangedArgs>(
                    h =>
                    {
                        if (topLevel.InsetsManager != null)
                        {
                            topLevel.InsetsManager.SafeAreaChanged += h;
                        }
                    },
                    h =>
                    {
                        if (topLevel.InsetsManager != null)
                        {
                            topLevel.InsetsManager.SafeAreaChanged -= h;
                        }
                    })
                .ObserveOn(AvaloniaScheduler.Instance)
                .Select(pattern => pattern.EventArgs.SafeAreaPadding)
                .BindTo(this, behavior => behavior.SafeAreaPadding);
        }
        
        return Disposable.Empty;
    }
    
    private Thickness safeAreaPadding;

    public static readonly DirectProperty<SafeAreaBehavior, Thickness> SafeAreaPaddingProperty = AvaloniaProperty.RegisterDirect<SafeAreaBehavior, Thickness>(
        nameof(SafeAreaPadding), o => o.SafeAreaPadding, (o, v) => o.SafeAreaPadding = v, defaultBindingMode: BindingMode.TwoWay);

    public Thickness SafeAreaPadding
    {
        get => safeAreaPadding;
        set => SetAndRaise(SafeAreaPaddingProperty, ref safeAreaPadding, value);
    }
}
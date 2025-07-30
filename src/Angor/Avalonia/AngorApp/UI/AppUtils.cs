using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Platform;

namespace AngorApp.UI;

public static class AppUtils
{
    public static IObservable<Thickness> SafeAreaPadding
    {
        get
        {
            var topLevel = TopLevel();
            var topLevelInsetsManager = topLevel?.InsetsManager;
            
            if (topLevelInsetsManager == null)
            {
                return Observable.Empty(new Thickness());
            }
            
            return Observable.FromEventPattern<SafeAreaChangedArgs>(h => topLevelInsetsManager.SafeAreaChanged += h,
                    h => topLevelInsetsManager.SafeAreaChanged -= h)
                .Select(pattern => pattern.EventArgs.SafeAreaPadding);
        }
    }

    public static TopLevel? TopLevel()
    {
        return Avalonia.Controls.TopLevel.GetTopLevel(CurrentView());
    }

    public static Visual? CurrentView()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            return desktopLifetime.MainWindow;
        }
        
        if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            return singleView.MainView;
        }
        
        throw new NotSupportedException("Unsupported application lifetime type.");
    }
}
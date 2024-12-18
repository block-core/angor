using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace AngorApp;

public static class Launcher
{
    public static readonly ICommand LaunchUriCommand = ReactiveCommand.CreateFromTask<string>(str => MainWindow().Launcher.LaunchUriAsync(new Uri(str)));

    private static TopLevel MainWindow()
    {
        if (Application.Current is null)
        {
            throw new InvalidOperationException("This application is not supported");
        }

        return Application.Current.ApplicationLifetime switch
        {
            null => throw new NotImplementedException(),
            IClassicDesktopStyleApplicationLifetime classicDesktopStyleApplicationLifetime => classicDesktopStyleApplicationLifetime.MainWindow!,
            ISingleViewApplicationLifetime singleViewApplicationLifetime => TopLevel.GetTopLevel(singleViewApplicationLifetime.MainView)!,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
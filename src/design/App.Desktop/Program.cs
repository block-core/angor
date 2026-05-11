using System;
using Avalonia;
using ReactiveUI.Avalonia;

namespace App.Desktop;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<global::App.App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI(b => b.WithAvalonia())
            .LogToTrace();
}

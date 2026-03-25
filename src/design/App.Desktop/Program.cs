using System;
using Avalonia;

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
            .LogToTrace();
}

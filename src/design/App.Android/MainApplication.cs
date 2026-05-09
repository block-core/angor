using System;
using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia;

namespace App.Android;

[Application]
public class MainApplication : AvaloniaAndroidApplication<global::App.App>
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        global::App.App.PlatformServices = services =>
        {
            services.AddSingleton<Angor.Sdk.Wallet.Infrastructure.Interfaces.ISecureKeyProvider, AndroidKeyStoreSecureKeyProvider>();
        };

        return base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .UseReactiveUI(b => b.WithAvalonia());
    }
}

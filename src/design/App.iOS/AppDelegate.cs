using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using Avalonia;
using Avalonia.iOS;
using Foundation;
using Microsoft.Extensions.DependencyInjection;
using UIKit;

namespace App.iOS;

[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<global::App.App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        global::App.App.PlatformServices = services =>
        {
            services.AddSingleton<ISecureKeyProvider, KeychainSecureKeyProvider>();
        };

        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}

using Angor.Contexts.CrossCutting;
using Angor.Contexts.Wallet.Infrastructure.Impl;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AngorApp.Composition.Registrations.Services;

public static class Security
{
    // Registers security-related services for wallet operations
    public static IServiceCollection AddSecurityContext(this IServiceCollection services)
    {
        services.AddSingleton<IWalletSecurityContext, WalletSecurityContext>();
        services.AddSingleton<IWalletEncryption, AesWalletEncryption>();
        services.AddSingleton<IPassphraseProvider, PassphraseProviderAdapter>();
        
        // Register platform-specific auto password store
#if WINDOWS
        services.AddSingleton<IAutoPasswordStore, Angor.Contexts.Integration.WalletFunding.PasswordStore.WindowsAutoPasswordStore>();
#elif ANDROID
        services.AddSingleton<IAutoPasswordStore, AngorApp.Android.PasswordStore.AndroidAutoPasswordStore>();
#elif IOS
        services.AddSingleton<IAutoPasswordStore, Angor.Contexts.Integration.WalletFunding.PasswordStore.IosAutoPasswordStore>();
#else
        // Linux and other platforms
        services.AddSingleton<IAutoPasswordStore, Angor.Contexts.Integration.WalletFunding.PasswordStore.LinuxAutoPasswordStore>();
#endif
        
        services.AddSingleton<IPasswordProvider, AutoPasswordProvider>();
        return services;
    }
}

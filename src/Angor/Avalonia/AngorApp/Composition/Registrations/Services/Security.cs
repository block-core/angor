using Angor.Contests.CrossCutting;
using Angor.Contexts.Wallet.Infrastructure.Impl;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using AngorApp.Model.Wallet.Password;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AngorApp.Composition.Registrations.Services;

public static class Security
{
    // Registers security-related services for wallet operations
    public static IServiceCollection AddSecurityContext(this IServiceCollection services, ProfileContext profileContext)
    {
        services.AddSingleton<IWalletSecurityContext, WalletSecurityContext>();
        services.AddSingleton<IWalletEncryption, AesWalletEncryption>();
        services.AddSingleton<IPassphraseProvider, PassphraseProviderAdapter>();
        //services.AddSingleton<IPasswordProvider, PasswordProviderAdapter>();
        services.AddSingleton<IPasswordProvider>(provider =>
            new FilePasswordProvider(
                provider.GetRequiredService<ISecureStorage>(),
                provider.GetRequiredService<IApplicationStorage>(),
                profileContext.AppName, // supply appName here
                profileContext.ProfileName, // supply profileName here
                provider.GetRequiredService<ILogger<FilePasswordProvider>>()));
        services.AddSingleton<ISecureStorage, SimpleSecureStorage>(); // Replace with platform-specific implementation if needed
        return services;
    }
}

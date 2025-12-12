using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Infrastructure.Impl;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using AngorApp.Model.Wallet.Password;
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
        //services.AddSingleton<IEncryptionKeyStore, WindowsWalletEncryptionKeyStore>();
        //services.AddSingleton<IPasswordProvider, LocalPasswordProvider>();
        services.AddSingleton<IPasswordProvider, PasswordProviderAdapter>();
        return services;
    }
}

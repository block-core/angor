using Angor.UI.Model.Implementation.Wallet;
using Angor.UI.Model.Implementation.Wallet.Password;
using Angor.Contexts.Wallet.Infrastructure.Impl;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AngorApp.Composition.Registrations;

public static class SecurityContext
{
    public static ServiceCollection Register(ServiceCollection container)
    {
        container.AddSingleton<IWalletSecurityContext, WalletSecurityContext>();
        container.AddSingleton<IWalletEncryption, AesWalletEncryption>();
        container.AddSingleton<IPassphraseProvider, PassphraseProviderAdapter>();
        container.AddSingleton<IEncryptionKeyProvider, EncryptionKeyProviderAdapter>();
        return container;
    }
}
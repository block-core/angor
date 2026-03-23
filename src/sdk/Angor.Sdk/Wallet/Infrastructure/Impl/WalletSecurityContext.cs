using Angor.Sdk.Wallet.Infrastructure.Interfaces;

namespace Angor.Sdk.Wallet.Infrastructure.Impl;

public class WalletSecurityContext(IPassphraseProvider passphraseProvider, IPasswordProvider passwordProvider)
    : IWalletSecurityContext
{
    public IPassphraseProvider PassphraseProvider { get; } = passphraseProvider;
    public IPasswordProvider PasswordProvider { get; } = passwordProvider;
}
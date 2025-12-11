using Angor.Contexts.Wallet.Infrastructure.Interfaces;

namespace Angor.Contexts.Wallet.Infrastructure.Impl;

public class WalletSecurityContext(IPassphraseProvider passphraseProvider, IPasswordProvider passwordProvider)
    : IWalletSecurityContext
{
    public IPassphraseProvider PassphraseProvider { get; } = passphraseProvider;
    public IPasswordProvider PasswordProvider { get; } = passwordProvider;
}
using Angor.Contexts.Wallet.Infrastructure.Interfaces;

namespace Angor.Contexts.Wallet.Infrastructure.Impl;

public class WalletSecurityContext(IWalletEncryption walletEncryption, IPassphraseProvider passphraseProvider, IPasswordProvider passwordProvider)
    : IWalletSecurityContext
{
    public IWalletEncryption WalletEncryption { get; } = walletEncryption;
    public IPassphraseProvider PassphraseProvider { get; } = passphraseProvider;
    public IPasswordProvider PasswordProvider { get; } = passwordProvider;
}
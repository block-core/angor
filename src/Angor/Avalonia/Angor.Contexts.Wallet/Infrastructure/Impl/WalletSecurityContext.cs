using Angor.Contexts.Wallet.Infrastructure.Interfaces;

namespace Angor.Contexts.Wallet.Infrastructure.Impl;

public class WalletSecurityContext(IWalletEncryption walletEncryption, IPassphraseProvider passphraseProvider, IEncryptionKeyProvider encryptionKeyProvider)
    : IWalletSecurityContext
{
    public IWalletEncryption WalletEncryption { get; } = walletEncryption;
    public IPassphraseProvider PassphraseProvider { get; } = passphraseProvider;
    public IEncryptionKeyProvider EncryptionKeyProvider { get; } = encryptionKeyProvider;
}
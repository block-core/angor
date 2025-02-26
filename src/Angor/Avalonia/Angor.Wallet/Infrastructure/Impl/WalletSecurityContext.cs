using Angor.Wallet.Infrastructure.Interfaces;

namespace Angor.Wallet.Infrastructure.Impl;

public class WalletSecurityContext(IWalletUnlockHandler walletUnlockHandler, IWalletEncryption walletEncryption, IPassphraseProvider passphraseProvider, IEncryptionKeyProvider encryptionKeyProvider)
    : IWalletSecurityContext
{
    public IWalletUnlockHandler WalletUnlockHandler { get; } = walletUnlockHandler;
    public IWalletEncryption WalletEncryption { get; } = walletEncryption;
    public IPassphraseProvider PassphraseProvider { get; } = passphraseProvider;
    public IEncryptionKeyProvider EncryptionKeyProvider { get; } = encryptionKeyProvider;
}
namespace Angor.Wallet.Infrastructure.Interfaces;

public interface IWalletSecurityContext
{
    IWalletUnlockHandler WalletUnlockHandler { get; }
    IWalletEncryption WalletEncryption { get; }
    IPassphraseProvider PassphraseProvider { get; }
    IEncryptionKeyProvider EncryptionKeyProvider { get; }
}
namespace Angor.Wallet.Infrastructure.Interfaces;

public interface IWalletSecurityContext
{
    IWalletEncryption WalletEncryption { get; }
    IPassphraseProvider PassphraseProvider { get; }
    IEncryptionKeyProvider EncryptionKeyProvider { get; }
}
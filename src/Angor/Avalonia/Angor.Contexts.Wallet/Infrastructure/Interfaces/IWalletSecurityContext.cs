namespace Angor.Contexts.Wallet.Infrastructure.Interfaces;

public interface IWalletSecurityContext
{
    IWalletEncryption WalletEncryption { get; }
    IPassphraseProvider PassphraseProvider { get; }
    IPasswordProvider PasswordProvider { get; }
}
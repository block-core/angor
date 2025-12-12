namespace Angor.Sdk.Wallet.Infrastructure.Interfaces;

public interface IWalletSecurityContext
{
    IPassphraseProvider PassphraseProvider { get; }
    IPasswordProvider PasswordProvider { get; }
}
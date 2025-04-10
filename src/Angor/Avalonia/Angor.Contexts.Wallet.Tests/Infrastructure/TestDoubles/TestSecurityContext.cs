using Angor.Contexts.Wallet.Infrastructure.Interfaces;

namespace Angor.Contexts.Wallet.Tests.Infrastructure;

public class TestSecurityContext : IWalletSecurityContext
{
    public IWalletEncryption WalletEncryption { get; }
    public IPassphraseProvider PassphraseProvider { get; }
    public IPasswordProvider PasswordProvider { get; }
}
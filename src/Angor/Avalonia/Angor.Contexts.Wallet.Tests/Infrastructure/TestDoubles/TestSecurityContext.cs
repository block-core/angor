using Angor.Sdk.Wallet.Infrastructure.Interfaces;

namespace Angor.Contexts.Wallet.Tests.Infrastructure.TestDoubles;

public class TestSecurityContext : IWalletSecurityContext
{
    public IWalletEncryption WalletEncryption { get; }
    public IPassphraseProvider PassphraseProvider { get; }
    public IPasswordProvider PasswordProvider { get; }
}
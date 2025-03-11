using Angor.Wallet.Infrastructure.Interfaces;

namespace Angor.Wallet.Tests.Infrastructure;

public class TestSecurityContext : IWalletSecurityContext
{
    public IWalletEncryption WalletEncryption { get; }
    public IPassphraseProvider PassphraseProvider { get; }
    public IEncryptionKeyProvider EncryptionKeyProvider { get; }
}
using Angor.Wallet.Infrastructure.Interfaces;

namespace Angor.Wallet.Tests.Infrastructure;

public class TestSecurityContext : IWalletSecurityContext
{
    public IWalletUnlockHandler WalletUnlockHandler { get; }
    public IWalletEncryption WalletEncryption { get; }
    public IPassphraseProvider PassphraseProvider { get; }
    public IEncryptionKeyProvider EncryptionKeyProvider { get; }
}
namespace Angor.Contexts.Wallet.Infrastructure.Impl;

public class EncryptedWallet
{
    public string Id { get; set; } // Changed from Guid to string for hash-based ID
    public string EncryptedData { get; set; }
    public string Salt { get; set; }
    public string IV { get; set; }
}
namespace Angor.Sdk.Wallet.Infrastructure.Impl;

public class EncryptedWallet
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string IV { get; set; }
    public string EncryptedData { get; set; }
    public string Salt { get; set; }
    public DateTime CreatedAt { get; set; }
}
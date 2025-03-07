namespace Angor.Wallet.Infrastructure.Impl;

public class EncryptedWallet
{
    public Guid Id { get; set; }
    public string EncryptedData { get; set; }
    public string Salt { get; set; }
    public string IV { get; set; }
}
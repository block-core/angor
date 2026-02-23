namespace Angor.Sdk.Wallet.Infrastructure.Impl;

public class EncryptedWallet
{
    public string Id { get; set; } = string.Empty;
    public string EncryptedData { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public string IV { get; set; } = string.Empty;
}
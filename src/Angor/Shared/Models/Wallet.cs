namespace Angor.Shared.Models;
public class Wallet
{
    public FounderKeyCollection FounderKeys { get; set; } = new();
    public string EncryptedData { get; set; } = string.Empty;
}
using System.Text.Json;

namespace Angor.Shared.Models;
public class Wallet
{
    public WalletWords WalletWords { get; set; } // todo: this will go away when we add passwords
    public FounderKeyCollection FounderKeys { get; set; }
    public WalletPayload Payload { get; set; }
}

public class WalletWords
{
    public string Words { get; set; }
    public string? Passphrase { get; set; }

    public string ConvertToString()
    {
        return JsonSerializer.Serialize(this);
    }

    public static WalletWords ConvertFromString(string data)
    {
        if (string.IsNullOrEmpty(data))
            throw new InvalidOperationException();

        return JsonSerializer.Deserialize<WalletWords>(data) ?? throw new InvalidOperationException();
    }
}

public class WalletPayload
{
    public string EncryptedData { get; set; }

}
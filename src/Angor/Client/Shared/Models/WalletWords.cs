using Blockcore.Utilities;

namespace Angor.Client.Shared.Models;

public class WalletWords
{
    public string Words { get; set; }
    public string? PassPhrase { get; set; }

    public string ConvertToString()
    {
        return System.Text.Json.JsonSerializer.Serialize(this);
    }

    public static WalletWords ConvertFromString(string data)
    {
        if (string.IsNullOrEmpty(data))
            throw new InvalidOperationException();

        return System.Text.Json.JsonSerializer.Deserialize<WalletWords>(data) ?? throw new InvalidOperationException();
    }
}
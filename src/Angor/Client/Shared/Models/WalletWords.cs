using Blockcore.Utilities;
using System.Text.Json;

namespace Angor.Client.Shared.Models;

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
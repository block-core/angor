namespace Angor.Client.Shared.Models;

public class SendInfo
{
    public Dictionary<string, UtxoData> SendUtxos { get; set; } = new();
}
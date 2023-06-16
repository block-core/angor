namespace Angor.Client.Shared.Models;

public class AccountInfo
{
    public string ExtPubKey { get; set; }
    public string Path { get; set; }
    public int LastFetchIndex { get; set; }
    public int LastFetchChangeIndex { get; set; }
    public long TotalBalance { get; set; }
    public Dictionary<string, List<UtxoData>> UtxoItems { get; set; } = new();
    public Dictionary<string, List<UtxoData>> UtxoChangeItems { get; set; } = new();

    public string GetNextReceiveAddress()
    {
        string? lastAddress = null;
        foreach (var item in this.UtxoItems)
        {
            lastAddress = item.Key;

            if (item.Value.Count == 0)
            {
                break;
            }
        }

        //if (lastAddress == null)
        //    throw new InvalidOperationException();

        return lastAddress;
    }

}
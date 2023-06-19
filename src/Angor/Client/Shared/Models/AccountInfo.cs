namespace Angor.Client.Shared.Models;

public class AccountInfo
{
    public string ExtPubKey { get; set; }
    public string Path { get; set; }
    public int LastFetchIndex { get; set; }
    public int LastFetchChangeIndex { get; set; }
    public long TotalBalance { get; set; }
    public Dictionary<string, AddressInfo> AddressesInfo { get; set; }
    public Dictionary<string, AddressInfo> ChangeAddressesInfo { get; set; }

    public string GetNextReceiveAddress()
    {
        string? lastAddress = null;
        foreach (var (address, addressInfo) in AddressesInfo)
        {
            lastAddress = address;

            if (!addressInfo.HasHistory)
            {
                break;
            }
        }

        //if (lastAddress == null)
        //    throw new InvalidOperationException();

        return lastAddress;
    }

}
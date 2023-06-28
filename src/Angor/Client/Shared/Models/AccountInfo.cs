namespace Angor.Client.Shared.Models;

public class AccountInfo
{
    public string ExtPubKey { get; set; }
    public string Path { get; set; }
    public int LastFetchIndex { get; set; }
    public int LastFetchChangeIndex { get; set; }
    public long TotalBalance { get; set; }
    public long TotalUnConfirmedBalance { get; set; }
    public List<AddressInfo> AddressesInfo { get; set; } = new();
    public List<AddressInfo> ChangeAddressesInfo { get; set; } = new();

    public string? GetNextReceiveAddress()
    {
        return AddressesInfo.Last()?.Address;
    }

}
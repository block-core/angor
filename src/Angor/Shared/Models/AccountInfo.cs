namespace Angor.Shared.Models;

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

    public List<UtxoData> PendingAdd { get; set; } = new();
    public List<UtxoData> PendingRemove { get; set; } = new();

    public int InvestmentsCount { get; set; } //TODO David handle the set logic
    
    public string? GetNextReceiveAddress()
    {
        return AddressesInfo.Last()?.Address;
    }

    public string? GetNextChangeReceiveAddress()
    {
        return ChangeAddressesInfo.Last()?.Address;
    }

    public void  CalculateBalance()
    {
            var balance = AddressesInfo.Concat(ChangeAddressesInfo).SelectMany(s => s.UtxoData).Sum(s => s.value);
            var balanceSpent = PendingRemove.Sum(s => s.value);
            TotalBalance = balance - balanceSpent;
        
            TotalUnConfirmedBalance = PendingAdd.Sum(s => s.value);
    }
}
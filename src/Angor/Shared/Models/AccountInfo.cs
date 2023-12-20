namespace Angor.Shared.Models;

public class AccountInfo
{
    public string ExtPubKey { get; set; }
    public string Path { get; set; }
    public int LastFetchIndex { get; set; }
    public int LastFetchChangeIndex { get; set; }
    public List<AddressInfo> AddressesInfo { get; set; } = new();
    public List<AddressInfo> ChangeAddressesInfo { get; set; } = new();

    public int InvestmentsCount { get; set; } //TODO David handle the set logic

    public IEnumerable<AddressInfo> AllAddresses()
    {
        foreach (var addressInfo in AddressesInfo.Concat(ChangeAddressesInfo))
        {
            yield return addressInfo;
        }
    }

    public string? GetNextReceiveAddress()
    {
        return AddressesInfo.Last()?.Address;
    }

    public string? GetNextChangeReceiveAddress()
    {
        return ChangeAddressesInfo.Last()?.Address;
    }

    public bool IsInPendingSpent(Outpoint outpoint)
    {
        return AllAddresses()
            .SelectMany(x => x.UtxoData)
            .Any(x => x.outpoint.ToString() == outpoint.ToString());
    }

    public bool RemoveInputFromPending(Outpoint outpoint)
    {
        foreach (var addressInfo in AddressesInfo)
        {
            var utxo = addressInfo.UtxoData.FirstOrDefault(x => x.outpoint.ToString() == outpoint.ToString());

            if (utxo is null) continue;
            addressInfo.UtxoData.Remove(utxo);
            return true;
        }
        
        foreach (var addressInfo in ChangeAddressesInfo)
        {
            var utxo = addressInfo.UtxoData.FirstOrDefault(x => x.outpoint.ToString() == outpoint.ToString());

            if (utxo is null) continue;
            addressInfo.UtxoData.Remove(utxo);
            return true;
        }

        return false;
    }
}
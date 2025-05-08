namespace Angor.Shared.Models;

public class AccountInfo
{
    public string ExtPubKey { get; set; }
    public string RootExtPubKey { get; set; }
    public string Path { get; set; }
    public int LastFetchIndex { get; set; }
    public int LastFetchChangeIndex { get; set; }
    public List<AddressInfo> AddressesInfo { get; set; } = new();
    public List<AddressInfo> ChangeAddressesInfo { get; set; } = new();

    public List<string> UtxoReservedForInvestment { get; set; } = new();

    public IEnumerable<AddressInfo> AllAddresses()
    {
        foreach (var addressInfo in AddressesInfo.Concat(ChangeAddressesInfo))
        {
            yield return addressInfo;
        }
    }

    public IEnumerable<UtxoData> AllUtxos()
    {
        foreach (var utxo in AllAddresses().SelectMany(_ => _.UtxoData))
        {
            yield return utxo;
        }
    }

    public string? GetNextReceiveAddress()
    {
        return AddressesInfo.LastOrDefault()?.Address;
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
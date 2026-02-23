namespace Angor.Shared.Models;

public class AccountInfo
{
    public string walletId { get; init; } = string.Empty;
    public string ExtPubKey { get; init; } = string.Empty;
    public string RootExtPubKey { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
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

    /// <summary>
    /// Adds new UTXOs to the specified address, avoiding duplicates.
    /// </summary>
    /// <param name="address">The address to add UTXOs to</param>
    /// <param name="newUtxos">The list of new UTXOs to add</param>
    /// <returns>The number of UTXOs actually added (excluding duplicates)</returns>
    public int AddNewUtxos(string address, List<UtxoData> newUtxos)
    {
        var addressInfo = AllAddresses().FirstOrDefault(a => a.Address == address);
        
        if (addressInfo == null)
            return 0;

        var addedCount = 0;
        foreach (var utxo in newUtxos)
        {
            // Check if UTXO already exists to avoid duplicates
            var existingUtxo = addressInfo.UtxoData
                .FirstOrDefault(u => u.outpoint.ToString() == utxo.outpoint.ToString());

            if (existingUtxo == null)
            {
                addressInfo.UtxoData.Add(utxo);
                addedCount++;
            }
        }

        return addedCount;
    }
}
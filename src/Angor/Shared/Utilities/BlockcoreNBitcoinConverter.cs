using Blockcore.Consensus.TransactionInfo;
using NBitcoin;
using Blockcore.NBitcoin;


namespace Angor.Shared.Utilities;

public class BlockcoreNBitcoinConverter : IBlockcoreNBitcoinConverter
{
    //todo: add conversions here..
    
    public NBitcoin.Network ConvertBlockcoreToNBitcoinNetwork(Blockcore.Networks.Network blockcoreNetwork)
    {
        if (blockcoreNetwork is null)
            throw new ArgumentNullException(nameof(blockcoreNetwork));

        // Match network by name or properties
        return blockcoreNetwork.Name switch
        {
            "Mainnet" => NBitcoin.Network.Main,
            "TestNet" => NBitcoin.Network.TestNet,
            "Regtest" => NBitcoin.Network.RegTest,
            _ => throw new NotSupportedException($"Network {blockcoreNetwork.Name} is not supported.")
        };
    }
    
    public NBitcoin.Transaction ConvertBlockcoreToNBitcoinTransaction(Blockcore.Consensus.TransactionInfo.Transaction blockcoreTransaction, Blockcore.Networks.Network blockcoreNetwork)
    {
        var nbitcoinNetwork = ConvertBlockcoreToNBitcoinNetwork(blockcoreNetwork);
        return NBitcoin.Transaction.Parse(blockcoreTransaction.ToHex(), nbitcoinNetwork);
    }
    
    public NBitcoin.BitcoinAddress ConvertBlockcoreAddressToNBitcoinAddress(Blockcore.Networks.Network blockcoreNetwork, string blockcoreAddress)
    {
        var nbitcoinNetwork = ConvertBlockcoreToNBitcoinNetwork(blockcoreNetwork);
        return NBitcoin.BitcoinAddress.Create(blockcoreAddress, nbitcoinNetwork);
    }
    

}
public interface IBlockcoreNBitcoinConverter
{
    NBitcoin.Network ConvertBlockcoreToNBitcoinNetwork(Blockcore.Networks.Network blockcoreNetwork);
    NBitcoin.Transaction ConvertBlockcoreToNBitcoinTransaction(Blockcore.Consensus.TransactionInfo.Transaction blockcoreTransaction, Blockcore.Networks.Network blockcoreNetwork);
    NBitcoin.BitcoinAddress ConvertBlockcoreAddressToNBitcoinAddress(Blockcore.Networks.Network blockcoreNetwork, string blockcoreAddress);
}
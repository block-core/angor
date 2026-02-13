using Angor.Shared.Models;
using Blockcore.NBitcoin;
using Blockcore.Networks;

namespace Angor.Shared.Services;

public class WalletSigner : IWalletSigner
{
    private readonly WalletWords _walletWords;
    private readonly Network _network;

    public WalletSigner(WalletWords walletWords, Network network)
    {
        _walletWords = walletWords;
        _network = network;
    }

    public Key GetPrivateKey(int accountIndex, int addressIndex, bool isChange)
    {
        var accountExtKey = GetAccountExtKey(accountIndex);
        var keyPath = new KeyPath($"{ (isChange ? 1 : 0) }/{addressIndex}");
        return accountExtKey.Derive(keyPath).PrivateKey;
    }

    public PubKey GetPublicKey(int accountIndex, int addressIndex, bool isChange)
    {
        return GetPrivateKey(accountIndex, addressIndex, isChange).PubKey;
    }

    public ExtKey GetAccountExtKey(int accountIndex)
    {
        var masterKey = GetMasterKey();
        
        // BIP84 for native segwit: m/84'/coin_type'/account'
        var purpose = 84;
        var coinType = _network.Consensus.CoinType;
        
        // Hardened derivation for purpose, coinType, and account
        var keyPath = new KeyPath($"{purpose}'/{coinType}'/{accountIndex}'");
        return masterKey.Derive(keyPath);
    }

    public Key GetPrivateKey(string hdPath)
    {
        var masterKey = GetMasterKey();
        return masterKey.Derive(new KeyPath(hdPath)).PrivateKey;
    }

    public string GetRootExtPubKey()
    {
        var masterKey = GetMasterKey();
        var rootExtPubKey = masterKey.Neuter();
        return rootExtPubKey.ToString(_network);
    }

    private ExtKey GetMasterKey()
    {
        var storage = new Blockcore.NBitcoin.Mnemonic(_walletWords.Words);
        return storage.DeriveExtKey();
    }
}

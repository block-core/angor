using System.Text;
using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP32;
using Blockcore.NBitcoin.Crypto;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.Networks;
using Microsoft.Extensions.Logging;

namespace Angor.Shared;

public class DerivationOperations : IDerivationOperations
{
    private readonly IHdOperations _hdOperations;
    private readonly ILogger<DerivationOperations> _logger;
    private readonly INetworkConfiguration _networkConfiguration;

    public DerivationOperations(IHdOperations hdOperations, ILogger<DerivationOperations> logger, INetworkConfiguration networkConfiguration)
    {
        _hdOperations = hdOperations;
        _logger = logger;
        _networkConfiguration = networkConfiguration;
    }
    
    private ExtKey GetExtendedKey(WalletWords walletWords)
    {
        ExtKey extendedKey;
        try
        {
            extendedKey = _hdOperations.GetExtendedKey(walletWords.Words, walletWords.Passphrase);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError("Exception occurred: {0}", ex.ToString());

            if (ex.Message == "Unknown")
                throw new Exception("Please make sure you enter valid mnemonic words.");

            throw;
        }

        return extendedKey;
    }

    public FounderKeyCollection DeriveProjectKeys(WalletWords walletWords, string angorTestKey)
    {
        FounderKeyCollection founderKeyCollection = new();

        for (int i = 1; i <= 15; i++)
        {
            var founderKey = DeriveFounderKey(walletWords, i);
            var founderRecoveryKey = DeriveFounderRecoveryKey(walletWords, founderKey);
            var projectIdentifier = DeriveAngorKey(angorTestKey, founderKey);
            var nostrPubKey = DeriveNostrPubKey(walletWords, founderKey);
            
            founderKeyCollection.Keys.Add(new FounderKeys
            {
                ProjectIdentifier = projectIdentifier,
                FounderRecoveryKey = founderRecoveryKey,
                FounderKey = founderKey,
                NostrPubKey = nostrPubKey, 
                Index = i
            });
        }

        return founderKeyCollection;

    }

    public FounderKeys GetProjectKey(FounderKeyCollection founderKeyCollection, int index)
    {
        var keys = founderKeyCollection.Keys.FirstOrDefault(k => k.Index == index);

        if (keys == null)
        {
            throw new Exception("Keys derivation limit exceeded");
        }

        return keys;

    }

    public string DeriveLeadInvestorSecretHash(WalletWords walletWords, string founderKey)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var upi = this.DeriveUniqueProjectIdentifier(founderKey);

        var path = $"m/5'/0'/{upi}'/2'";

        ExtPubKey extPubKey = _hdOperations.GetExtendedPublicKey(extendedKey.PrivateKey, extendedKey.ChainCode, path);

        var derivedSecret = extendedKey.Derive(new KeyPath(path));

        var hash = Hashes.Hash256(derivedSecret.ToBytes()).ToString();

        return hash;
    }

    public string DeriveInvestorKey(WalletWords walletWords, string founderKey)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var upi = this.DeriveUniqueProjectIdentifier(founderKey);

        var path = $"m/5'/0'/{upi}'/3'";

        ExtPubKey extPubKey = _hdOperations.GetExtendedPublicKey(extendedKey.PrivateKey, extendedKey.ChainCode, path);

        return extPubKey.PubKey.ToHex();
    }

    public Key DeriveInvestorPrivateKey(WalletWords walletWords, string founderKey)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var upi = this.DeriveUniqueProjectIdentifier(founderKey);

        var path = $"m/5'/0'/{upi}'/3'";

        ExtPubKey extPubKey = _hdOperations.GetExtendedPublicKey(extendedKey.PrivateKey, extendedKey.ChainCode, path);

        ExtKey extKey = extendedKey.Derive(new KeyPath(path));

        return extKey.PrivateKey;
    }

    public string DeriveFounderKey(WalletWords walletWords, int index)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var path = $"m/5'/{index}'";

        ExtPubKey extPubKey = _hdOperations.GetExtendedPublicKey(extendedKey.PrivateKey, extendedKey.ChainCode, path);

        return extPubKey.PubKey.ToHex();
    }
    
    public string DeriveNostrPubKey(WalletWords walletWords, string founderKey)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var upi = this.DeriveUniqueProjectIdentifier(founderKey);

        var path = $"m/44'/1237'/{upi}'/0/0";

        ExtKey extKey = extendedKey.Derive(new KeyPath(path));

        return extKey.PrivateKey.PubKey.ToHex()[2..]; //Need the pub key without prefix TODO find a better way to get the Schnorr pub key
    }

    public string DeriveFounderRecoveryKey(WalletWords walletWords, string founderKey)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var upi = this.DeriveUniqueProjectIdentifier(founderKey);

        var path = $"m/5'/0'/{upi}'/1'";

        ExtPubKey extPubKey = _hdOperations.GetExtendedPublicKey(extendedKey.PrivateKey, extendedKey.ChainCode, path);

        return extPubKey.PubKey.ToHex();
    }

    public Key DeriveFounderPrivateKey(WalletWords walletWords, int index)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var path = $"m/5'/{index}'";

        ExtKey extKey = extendedKey.Derive(new KeyPath(path));

        return extKey.PrivateKey;
    }

    public Key DeriveFounderRecoveryPrivateKey(WalletWords walletWords, string founderKey)
    {
        ExtKey extendedKey;

        try
        {
            extendedKey = _hdOperations.GetExtendedKey(walletWords.Words, walletWords.Passphrase);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError("Exception occurred: {0}", ex.ToString());

            if (ex.Message == "Unknown")
                throw new Exception("Please make sure you enter valid mnemonic words.");

            throw;
        }

        var upi = this.DeriveUniqueProjectIdentifier(founderKey);

        var path = $"m/5'/0'/{upi}'/1'";

        ExtKey extKey = extendedKey.Derive(new KeyPath(path));

        return extKey.PrivateKey;
    }

    public Key DeriveProjectNostrPrivateKey(WalletWords walletWords, string founderKey)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var upi = this.DeriveUniqueProjectIdentifier(founderKey);

        var path = $"m/44'/1237'/{upi}'/0/0";

        ExtKey extKey = extendedKey.Derive(new KeyPath(path));

        return extKey.PrivateKey;
    }
    
    public async Task<Key> DeriveProjectNostrPrivateKeyAsync(WalletWords walletWords, string founderKey)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);
       
        var upi = this.DeriveUniqueProjectIdentifier(founderKey);

        var path = $"m/44'/1237'/{upi}'/0/0";

        var extKey = await Task.Run(() => extendedKey.Derive(new KeyPath(path)));
        
        return extKey.PrivateKey;
    }

    public uint DeriveUniqueProjectIdentifier(string founderKey)
    {
        ExtKey.UseBCForHMACSHA512 = true;
        Hashes.UseBCForHMACSHA512 = true;

        var key = new PubKey(founderKey);

        var hashOfid = Hashes.Hash256(key.ToBytes());

        var upi = (uint)(hashOfid.GetLow64() & int.MaxValue);
        
        _logger.LogInformation($"Unique Project Identifier - founderKey = {founderKey}, hashOfFounderKey = {hashOfid}, hashOfFounderKeyCastToInt = {upi}");
        
        if (upi >= 2_147_483_648)
            throw new Exception();
        
        return upi;
    }

    public string DeriveNostrStoragePubKeyHex(WalletWords walletWords)
    {
        var key = DeriveNostrStorageKey(walletWords);

        return key.PubKey.ToHex()[2..]; //Need the pub key without prefix
    }

    public Key DeriveNostrStorageKey(WalletWords walletWords)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var path = $"m/44'/1237'/1'/0/0";

        ExtKey extKey = extendedKey.Derive(new KeyPath(path));

        return extKey.PrivateKey;
    }

    public string DeriveNostrStoragePassword(WalletWords walletWords)
    {
        var key = DeriveNostrStorageKey(walletWords);

         var privateKeyBytes = key.ToBytes();

        var hashedKey = Hashes.Hash256(new Span<byte>(privateKeyBytes));

        // the hex of the hash of the private key is the password
        var hex = Encoders.Hex.EncodeData(hashedKey.ToArray()).Replace("-", "").ToLower();

        return hex;
    }

    public string DeriveAngorKey(string angorRootKey, string founderKey)
    {
        Network network = _networkConfiguration.GetNetwork();

        var extKey = new BitcoinExtPubKey(angorRootKey, network).ExtPubKey;

        var upi = this.DeriveUniqueProjectIdentifier(founderKey);

        var angorKey = extKey.Derive(upi).PubKey;
        
        var encoder = new Bech32Encoder("angor");

        var address = encoder.Encode(0, angorKey.WitHash.ToBytes());

        _logger.LogInformation($"DeriveAngorKey - angorRootKey = {angorRootKey}, founderKey = {founderKey}, upi = {upi}, angorKey = {angorKey}, angorKeyWitHash = {angorKey.WitHash}, address = {address}");

        return address;
    }

    public Script AngorKeyToScript(string angorKey)
    {
        var encoder = new Bech32Encoder("angor");

        var data = encoder.Decode(angorKey, out byte ver);

        var wit = new WitKeyId(data);

        return wit.ScriptPubKey;
    }
}
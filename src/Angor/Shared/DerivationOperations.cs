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
            var founderRecoveryKey = DeriveFounderRecoveryKey(walletWords, i);
            var projectIdentifier = DeriveAngorKey(founderKey, angorTestKey);
            var nostrPubKey = DeriveNostrPubKey(walletWords, i);
            
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

    public string DeriveSeederSecretHash(WalletWords walletWords, string founderKey)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var projectid = this.DeriveProjectId(founderKey);

        var path = $"m/5'/{projectid}'/4'";

        ExtPubKey extPubKey = _hdOperations.GetExtendedPublicKey(extendedKey.PrivateKey, extendedKey.ChainCode, path);

        var derivedSecret = extendedKey.Derive(new KeyPath(path));

        var hash = Hashes.Hash256(derivedSecret.ToBytes()).ToString();

        return hash;
    }

    public string DeriveInvestorKey(WalletWords walletWords, string founderKey)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var projectid = this.DeriveProjectId(founderKey);

        var path = $"m/5'/{projectid}'/1'";

        ExtPubKey extPubKey = _hdOperations.GetExtendedPublicKey(extendedKey.PrivateKey, extendedKey.ChainCode, path);

        return extPubKey.PubKey.ToHex();
    }

    public Key DeriveInvestorPrivateKey(WalletWords walletWords, string founderKey)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var projectid = this.DeriveProjectId(founderKey);

        var path = $"m/5'/{projectid}'/1'";

        ExtPubKey extPubKey = _hdOperations.GetExtendedPublicKey(extendedKey.PrivateKey, extendedKey.ChainCode, path);

        ExtKey extKey = extendedKey.Derive(new KeyPath(path));

        return extKey.PrivateKey;
    }

    public string DeriveFounderKey(WalletWords walletWords, int index)
    {
        // founder key is derived from the path m/5'
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var path = $"m/5'/{index}'";

        ExtPubKey extPubKey = _hdOperations.GetExtendedPublicKey(extendedKey.PrivateKey, extendedKey.ChainCode, path);

        return extPubKey.PubKey.ToHex();
    }
    
    public string DeriveNostrPubKey(WalletWords walletWords, int index)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var path = $"m/44'/1237'/{index}/0/0";

        ExtKey extKey = extendedKey.Derive(new KeyPath(path));

        return extKey.PrivateKey.PubKey.ToHex()[2..]; //Need the pub key without prefix TODO find a better way to get the Schnorr pub key
    }

    public string DeriveFounderRecoveryKey(WalletWords walletWords, int index)
    {
        // founder recovery key is derived from the path m/6'

        Network network = _networkConfiguration.GetNetwork();

        ExtKey extendedKey = GetExtendedKey(walletWords);

        var path = $"m/6'/{index}'";

        ExtPubKey extPubKey = _hdOperations.GetExtendedPublicKey(extendedKey.PrivateKey, extendedKey.ChainCode, path);

        return extPubKey.PubKey.ToHex();
    }

    public Key DeriveFounderPrivateKey(WalletWords walletWords, int index)
    {
        // founder key is derived from the path m/5'
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var path = $"m/5'/{index}'";

        ExtKey extKey = extendedKey.Derive(new KeyPath(path));

        return extKey.PrivateKey;
    }

    public Key DeriveFounderRecoveryPrivateKey(WalletWords walletWords, int index)
    {
        // founder key is derived from the path m/5'


        Network network = _networkConfiguration.GetNetwork();


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

        var path = $"m/6'/{index}'";

        ExtKey extKey = extendedKey.Derive(new KeyPath(path));

        return extKey.PrivateKey;
    }
    
    public Key DeriveProjectNostrPrivateKey(WalletWords walletWords, int index)
    {
        // founder key is derived from the path m/5'
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var path = $"m/44'/1237'/{index}/0/0";

        ExtKey extKey = extendedKey.Derive(new KeyPath(path));

        return extKey.PrivateKey;
    }

    public uint DeriveProjectId(string founderKey)
    {
        ExtKey.UseBCForHMACSHA512 = true;
        Blockcore.NBitcoin.Crypto.Hashes.UseBCForHMACSHA512 = true;

        Network network = _networkConfiguration.GetNetwork();

        var key = new PubKey(founderKey);

        var hashOfid = Hashes.Hash256(key.ToBytes());

        var projectid = hashOfid.GetLow32();

        var ret = projectid / 2; // the max size of bip32 derivation range is 2,147,483,648 (2^31) the max number of uint is 4,294,967,295 so we must divide by two

        if (ret >= 2_147_483_648)
            throw new Exception();

        return ret;
    }

    public string DeriveAngorKey(string founderKey, string angorRootKey)
    {
        Network network = _networkConfiguration.GetNetwork();

        var extKey = new BitcoinExtPubKey(angorRootKey, network).ExtPubKey;

        var projectid = this.DeriveProjectId(founderKey);

        var path = $"{projectid}";

        var angorKey = extKey.Derive(projectid).PubKey;
        
        var encoder = new Bech32Encoder("angor");

        var address = encoder.Encode(0, angorKey.WitHash.ToBytes());

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
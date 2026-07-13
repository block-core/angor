using System.Text;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
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
        try
        {
            return walletWords.GetOrDeriveExtKey(_hdOperations);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError("Exception occurred: {0}", ex.ToString());

            if (ex.Message == "Unknown")
                throw new Exception("Please make sure you enter valid mnemonic words.");

            throw;
        }
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
        return DeriveLeadInvestorSecretHash(walletWords, founderKey, projectVersion: 1);
    }

    public string DeriveLeadInvestorSecretHash(WalletWords walletWords, string founderKey, int projectVersion)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var subPath = BuildProjectSubPath(founderKey, projectVersion);
        var path = $"m/5'/0'/{subPath}/2'";

        ExtPubKey extPubKey = _hdOperations.GetExtendedPublicKey(extendedKey.PrivateKey, extendedKey.ChainCode, path);

        var derivedSecret = extendedKey.Derive(new KeyPath(path));

        var hash = Hashes.DoubleSHA256(derivedSecret.ToBytes()).ToString();

        return hash;
    }

    public string DeriveInvestorKey(WalletWords walletWords, string founderKey)
    {
        return DeriveInvestorKey(walletWords, founderKey, projectVersion: 1);
    }

    public string DeriveInvestorKey(WalletWords walletWords, string founderKey, int projectVersion)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var subPath = BuildProjectSubPath(founderKey, projectVersion);
        var path = $"m/5'/0'/{subPath}/3'";

        ExtPubKey extPubKey = _hdOperations.GetExtendedPublicKey(extendedKey.PrivateKey, extendedKey.ChainCode, path);

        return extPubKey.PubKey.ToHex();
    }

    public AngorKey DeriveInvestorPrivateKey(WalletWords walletWords, string founderKey)
    {
        return DeriveInvestorPrivateKey(walletWords, founderKey, projectVersion: 1);
    }

    public AngorKey DeriveInvestorPrivateKey(WalletWords walletWords, string founderKey, int projectVersion)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var subPath = BuildProjectSubPath(founderKey, projectVersion);
        var path = $"m/5'/0'/{subPath}/3'";

        ExtPubKey extPubKey = _hdOperations.GetExtendedPublicKey(extendedKey.PrivateKey, extendedKey.ChainCode, path);

        ExtKey extKey = extendedKey.Derive(new KeyPath(path));

        return AngorKey.From(extKey.PrivateKey);
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
        return DeriveNostrPubKey(walletWords, founderKey, projectVersion: 1);
    }

    public string DeriveNostrPubKey(WalletWords walletWords, string founderKey, int projectVersion)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var subPath = BuildProjectSubPath(founderKey, projectVersion);
        var path = $"m/44'/1237'/{subPath}/0/0";

        ExtKey extKey = extendedKey.Derive(new KeyPath(path));

        return extKey.PrivateKey.PubKey.ToHex()[2..]; //Need the pub key without prefix TODO find a better way to get the Schnorr pub key
    }

    public string DeriveFounderRecoveryKey(WalletWords walletWords, string founderKey)
    {
        return DeriveFounderRecoveryKey(walletWords, founderKey, projectVersion: 1);
    }

    public string DeriveFounderRecoveryKey(WalletWords walletWords, string founderKey, int projectVersion)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var subPath = BuildProjectSubPath(founderKey, projectVersion);
        var path = $"m/5'/0'/{subPath}/1'";

        ExtPubKey extPubKey = _hdOperations.GetExtendedPublicKey(extendedKey.PrivateKey, extendedKey.ChainCode, path);

        return extPubKey.PubKey.ToHex();
    }

    public AngorKey DeriveFounderPrivateKey(WalletWords walletWords, int index)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var path = $"m/5'/{index}'";

        ExtKey extKey = extendedKey.Derive(new KeyPath(path));

        return AngorKey.From(extKey.PrivateKey);
    }

    public AngorKey DeriveFounderRecoveryPrivateKey(WalletWords walletWords, string founderKey)
    {
        return DeriveFounderRecoveryPrivateKey(walletWords, founderKey, projectVersion: 1);
    }

    public AngorKey DeriveFounderRecoveryPrivateKey(WalletWords walletWords, string founderKey, int projectVersion)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var subPath = BuildProjectSubPath(founderKey, projectVersion);
        var path = $"m/5'/0'/{subPath}/1'";

        ExtKey extKey = extendedKey.Derive(new KeyPath(path));

        return AngorKey.From(extKey.PrivateKey);
    }

    public AngorKey DeriveProjectNostrPrivateKey(WalletWords walletWords, string founderKey)
    {
        return DeriveProjectNostrPrivateKey(walletWords, founderKey, projectVersion: 1);
    }

    public AngorKey DeriveProjectNostrPrivateKey(WalletWords walletWords, string founderKey, int projectVersion)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var subPath = BuildProjectSubPath(founderKey, projectVersion);
        var path = $"m/44'/1237'/{subPath}/0/0";

        ExtKey extKey = extendedKey.Derive(new KeyPath(path));

        return AngorKey.From(extKey.PrivateKey);
    }
    
    public async Task<AngorKey> DeriveProjectNostrPrivateKeyAsync(WalletWords walletWords, string founderKey)
    {
        return await DeriveProjectNostrPrivateKeyAsync(walletWords, founderKey, projectVersion: 1);
    }

    public async Task<AngorKey> DeriveProjectNostrPrivateKeyAsync(WalletWords walletWords, string founderKey, int projectVersion)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);
       
        var subPath = BuildProjectSubPath(founderKey, projectVersion);
        var path = $"m/44'/1237'/{subPath}/0/0";

        var extKey = await Task.Run(() => extendedKey.Derive(new KeyPath(path)));
        
        return AngorKey.From(extKey.PrivateKey);
    }

    public uint DeriveUniqueProjectIdentifier(string founderKey)
    {
        var key = new PubKey(founderKey);

        var hashOfid = Hashes.DoubleSHA256(key.ToBytes());

        var upi = (uint)(hashOfid.GetLow64() & int.MaxValue);
        
        _logger.LogDebug("UPI derived: {Upi} for founderKey={FounderKey}", upi, founderKey);
        
        if (upi >= 2_147_483_648)
            throw new Exception();
        
        return upi;
    }

    /// <summary>
    /// Derives a two-level project sub-path with 62 bits of entropy (two 31-bit indices).
    /// Used for Version 3+ projects to reduce collision probability from ~46k to ~2 billion projects.
    /// </summary>
    public (uint Hi, uint Lo) DeriveProjectIndicesV2(string founderKey)
    {
        var key = new PubKey(founderKey);
        var hashOfid = Hashes.Hash256(key.ToBytes());
        var low64 = hashOfid.GetLow64();

        // Split 62 bits into two 31-bit indices (each valid for BIP-32 hardened derivation)
        var hi = (uint)(low64 & int.MaxValue);                   // bits 0-30
        var lo = (uint)((low64 >> 31) & int.MaxValue);           // bits 31-61

        return (hi, lo);
    }

    /// <summary>
    /// Returns the project derivation sub-path component based on the project version.
    /// V1/V2: single level "{upi}'" (31-bit entropy)
    /// V3+:   two levels "{hi}'/{lo}'" (62-bit entropy)
    /// </summary>
    private string BuildProjectSubPath(string founderKey, int projectVersion = 1)
    {
        if (projectVersion >= 3)
        {
            var (hi, lo) = DeriveProjectIndicesV2(founderKey);
            return $"{hi}'/{lo}'";
        }

        var upi = DeriveUniqueProjectIdentifier(founderKey);
        return $"{upi}'";
    }

    public string DeriveNostrStoragePubKeyHex(WalletWords walletWords)
    {
        var key = DeriveNostrStorageKey(walletWords);

        return key.PubKey.ToHex()[2..]; //Need the pub key without prefix
    }

    public AngorKey DeriveNostrStorageKey(WalletWords walletWords)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);

        var networkIndex = GetNetworkStorageIndex();
        var path = $"m/44'/1237'/1'/{networkIndex}/0";

        ExtKey extKey = extendedKey.Derive(new KeyPath(path));

        return AngorKey.From(extKey.PrivateKey);
    }

    /// <summary>
    /// Returns a network-specific index for the nostr storage derivation path.
    /// Mainnet uses index 0 for backward compatibility with existing relay data.
    /// All other networks (testnet, signet, angornet, etc.) use index 1 to isolate
    /// them from mainnet and prevent cross-network data contamination.
    /// </summary>
    private int GetNetworkStorageIndex()
    {
        var network = _networkConfiguration.GetNetwork();
        return network.Name == "Main" ? 0 : 1;
    }

    public AngorKey DeriveSupportDmKey(WalletWords walletWords)
    {
        ExtKey extendedKey = GetExtendedKey(walletWords);
        var networkIndex = GetNetworkStorageIndex();
        var path = $"m/44'/1237'/2'/{networkIndex}/0";
        return AngorKey.From(extendedKey.Derive(new KeyPath(path)).PrivateKey);
    }

    public string DeriveSupportDmPubKeyHex(WalletWords walletWords)
    {
        var key = DeriveSupportDmKey(walletWords);
        return key.PubKey.ToHex()[2..];
    }

    public string DeriveNostrStoragePassword(WalletWords walletWords)
    {
        var key = DeriveNostrStorageKey(walletWords);

        var privateKeyBytes = key.ToBytes();

        var hashedKey = Hashes.DoubleSHA256(privateKeyBytes);

        // the hex of the hash of the private key is the password
        var hex = Encoders.Hex.EncodeData(hashedKey.ToBytes(false)).Replace("-", "").ToLower();

        return hex;
    }

    public string DeriveAngorKey(string angorRootKey, string founderKey)
    {
        return DeriveAngorKey(angorRootKey, founderKey, projectVersion: 1);
    }

    public string DeriveAngorKey(string angorRootKey, string founderKey, int projectVersion)
    {
        AngorNetwork network = _networkConfiguration.GetNetwork();

        var extKey = new BitcoinExtPubKey(angorRootKey, network.BitcoinNetwork).ExtPubKey;

        if (projectVersion >= 3)
        {
            var (hi, lo) = DeriveProjectIndicesV2(founderKey);
            var angorKey = extKey.Derive(hi).Derive(lo).PubKey;

            var encoder = Encoders.Bech32("angor");
            var address = encoder.Encode(0, angorKey.WitHash.ToBytes());

            _logger.LogDebug("DeriveAngorKey V2 - founderKey={FounderKey}, hi={Hi}, lo={Lo}, address={Address}", founderKey, hi, lo, address);
            return address;
        }

        var upi = this.DeriveUniqueProjectIdentifier(founderKey);
        var legacyAngorKey = extKey.Derive(upi).PubKey;

        var legacyEncoder = Encoders.Bech32("angor");
        var legacyAddress = legacyEncoder.Encode(0, legacyAngorKey.WitHash.ToBytes());

        _logger.LogDebug("DeriveAngorKey - founderKey={FounderKey}, upi={Upi}, address={Address}", founderKey, upi, legacyAddress);

        return legacyAddress;
    }

    public Script AngorKeyToScript(string angorKey)
    {
        var encoder = Encoders.Bech32("angor");

        var data = encoder.Decode(angorKey, out byte ver);

        var wit = new WitKeyId(data);

        return wit.ScriptPubKey;
    }

    public string ConvertAngorKeyToBitcoinAddress(string projectId)
    {
        AngorNetwork network = _networkConfiguration.GetNetwork();

        // Decode the angor address to get the witness program
        var angorEncoder = Encoders.Bech32("angor");
        var data = angorEncoder.Decode(projectId, out byte witnessVersion);

        // Re-encode using the network's address format
        var wit = new WitKeyId(data);
        var bitcoinAddress = wit.GetAddress(network.BitcoinNetwork).ToString();

        _logger.LogDebug("ConvertAngorKeyToBitcoinAddress - projectId={ProjectId}, address={Address}", projectId, bitcoinAddress);

        return bitcoinAddress;
    }
}

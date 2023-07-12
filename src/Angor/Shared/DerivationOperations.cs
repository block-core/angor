using System.Net.Http.Json;
using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP32;
using Blockcore.NBitcoin.BIP39;
using Blockcore.NBitcoin.Crypto;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.Networks;
using Microsoft.Extensions.Logging;

namespace Angor.Shared;

public class DerivationOperations //: IDerivationOperations
{
    private readonly HttpClient _http;
    private readonly IHdOperations _hdOperations;
    private readonly ILogger<DerivationOperations> _logger;
    private readonly INetworkConfiguration _networkConfiguration;

    private const int AccountIndex = 0; // for now only account 0
    private const int Purpose = 84; // for now only legacy

    public DerivationOperations(HttpClient http, IHdOperations hdOperations, ILogger<DerivationOperations> logger, INetworkConfiguration networkConfiguration)
    {
        _http = http;
        _hdOperations = hdOperations;
        _logger = logger;
        _networkConfiguration = networkConfiguration;
    }


    public string DeriveFounderKey(WalletWords walletWords)
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

        var path = $"m/5'";

        ExtPubKey extPubKey = _hdOperations.GetExtendedPublicKey(extendedKey.PrivateKey, extendedKey.ChainCode, path);

        return extPubKey.PubKey.ToHex();
    }

    public uint DeriveProjectId(string founderKey)
    {
        ExtKey.UseBCForHMACSHA512 = true;
        Blockcore.NBitcoin.Crypto.Hashes.UseBCForHMACSHA512 = true;

        Network network = _networkConfiguration.GetNetwork();


        var key = new PubKey(founderKey);

        var hashOfid = Hashes.Hash256(key.ToBytes());

        var projectid = hashOfid.GetLow32();

        var ret = projectid / 2; // the max size of bip32 derivation range is 2,147,483,648 (2^31) the max number of uint is 4,294,967,295 so we must divide by zero

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

    public Script DeriveAngorKeyScript(string angorKey)
    {
        Network network = _networkConfiguration.GetNetwork();

        var encoder = new Bech32Encoder("angor");

        var data = encoder.Decode(angorKey, out byte ver);

        var wit = new WitKeyId(data);

        return wit.ScriptPubKey;
    }
}
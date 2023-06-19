using System.Net.Http.Json;
using Angor.Client.Shared.Models;
using Angor.Client.Shared.Types;
using Angor.Client.Storage;
using Angor.Shared;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP32;
using Blockcore.NBitcoin.BIP39;
using Blockcore.Networks;

namespace Angor.Client.Services;

public class WalletOperations : IWalletOperations 
{
    private readonly HttpClient _http;
    private readonly IClientStorage _storage;
    private readonly IWalletStorage _walletStorage;
    private readonly IHdOperations _hdOperations;
    private readonly ILogger<WalletOperations> _logger;
    private readonly INetworkConfiguration _networkConfiguration;

    public WalletOperations(HttpClient http, IClientStorage storage, IHdOperations hdOperations, ILogger<WalletOperations> logger, INetworkConfiguration networkConfiguration, IWalletStorage walletStorage)
    {
        _http = http;
        _storage = storage;
        _hdOperations = hdOperations;
        _logger = logger;
        _networkConfiguration = networkConfiguration;
        _walletStorage = walletStorage;
    }

    public string GenerateWalletWords()
    {
        var count = (WordCount)12;
        var mnemonic = new Mnemonic(Wordlist.English, count);
        string walletWords = mnemonic.ToString();
        return walletWords;
    }

    public async Task<(bool, string)> SendAmountToAddress(decimal sendAmount, long selectedFee, string sendToAddress)
    {
        Network network = _networkConfiguration.GetNetwork();
        AccountInfo accountInfo = _storage.GetAccountInfo(network.Name);

        var utxos = new List<AddressInfo>();
        utxos.AddRange(accountInfo.AddressesInfo.Values);
        utxos.AddRange(accountInfo.ChangeAddressesInfo.Values);

        var utxosToSpend = new List<(string,UtxoData)>();

        long ToSendSats = Money.Coins(sendAmount).Satoshi;

        long total = 0;
        foreach (var utxoData in utxos.SelectMany(_ => _.UtxoData
                         .Select(u =>  new{path = _.HdPath, utxo = u }))
                     .OrderBy(o => o.utxo.blockIndex)
                     .ThenByDescending(o => o.utxo.value))
        {
            utxosToSpend.Add((utxoData.path,utxoData.utxo));

            total += utxoData.utxo.value;

            if (total > ToSendSats)
            {
                break;
            }
        }

        if (total < ToSendSats)
            return (false, "not enough funds");

        ExtKey extendedKey;
        try
        {
            var data = _walletStorage.GetWallet();
            extendedKey = _hdOperations.GetExtendedKey(data.Words, data.Passphrase);
        }
        catch (NotSupportedException ex)
        {
            Console.WriteLine("Exception occurred: {0}", ex.ToString());

            if (ex.Message == "Unknown")
                throw new Exception("Please make sure you enter valid mnemonic words.");

            throw;
        }

        var coins = new List<Coin>();
        var keys = new List<Key>();

        foreach (var (hdPath, utxo) in utxosToSpend)
        {
            coins.Add(new Coin(uint256.Parse(utxo.outpoint.transactionId), (uint)utxo.outpoint.outputIndex,
                Money.Satoshis(utxo.value), Script.FromHex(utxo.scriptHex)));

            // derive the private key
            var extKey = extendedKey.Derive(new KeyPath(hdPath));
            Key privateKey = extKey.PrivateKey;
            keys.Add(privateKey);
        }

        var change = accountInfo.ChangeAddressesInfo.First(f => f.Value.HasHistory == false).Key;

        var builder = new TransactionBuilder(network)
            .Send(BitcoinWitPubKeyAddress.Create(sendToAddress, network), Money.Coins(sendAmount))
            .AddCoins(coins)
            .AddKeys(keys.ToArray())
            .SetChange(BitcoinWitPubKeyAddress.Create(change, network))
            .SendEstimatedFees(new FeeRate(Money.Satoshis(selectedFee)));

        var signedTransaction = builder.BuildTransaction(true);

        var hex = signedTransaction.ToHex(network.Consensus.ConsensusFactory);

        var indexer = _networkConfiguration.getIndexerUrl();
        
        var endpoint = Path.Combine(indexer.Url, "command/send");

        var res = await _http.PostAsync(endpoint, new StringContent(hex));

        if (res.IsSuccessStatusCode)
            return (true,signedTransaction.GetHash().ToString());

        var content = await res.Content.ReadAsStringAsync();
        
        return (false, res.ReasonPhrase + content);
    }

    public void BuildAccountInfoForWalletWords()
    {
        ExtKey.UseBCForHMACSHA512 = true;
        Blockcore.NBitcoin.Crypto.Hashes.UseBCForHMACSHA512 = true;

        Network network = _networkConfiguration.GetNetwork();
        var coinType = network.Consensus.CoinType;
        var accountIndex = 0; // for now only account 0
        var purpose = 84; // for now only legacy

        AccountInfo accountInfo = _storage.GetAccountInfo(network.Name);

        if (accountInfo != null)
            return;

        accountInfo = new AccountInfo();

        ExtKey extendedKey;
        try
        {
            var data = _walletStorage.GetWallet();
            extendedKey = _hdOperations.GetExtendedKey(data.Words, data.Passphrase);
        }
        catch (NotSupportedException ex)
        {
            Console.WriteLine("Exception occurred: {0}", ex.ToString());

            if (ex.Message == "Unknown")
                throw new Exception("Please make sure you enter valid mnemonic words.");

            throw;
        }

        string accountHdPath = _hdOperations.GetAccountHdPath(purpose, coinType, accountIndex);
        Key privateKey = extendedKey.PrivateKey;
        _storage.SetWalletPubkey(privateKey.PubKey.ToHex());

        ExtPubKey accountExtPubKeyTostore =
            _hdOperations.GetExtendedPublicKey(privateKey, extendedKey.ChainCode, accountHdPath);

        accountInfo.ExtPubKey = accountExtPubKeyTostore.ToString(network);
        accountInfo.Path = accountHdPath;

        _storage.SetAccountInfo(network.Name, accountInfo);
    }

    public async Task<AccountInfo> UpdateAccountInfo()
    {
        ExtKey.UseBCForHMACSHA512 = true;
        Blockcore.NBitcoin.Crypto.Hashes.UseBCForHMACSHA512 = true;

        Network network = _networkConfiguration.GetNetwork();

        AccountInfo accountInfo = _storage.GetAccountInfo(network.Name);

        var (index, items) = await FetcAddressesDataForPubKeyAsync(accountInfo.LastFetchIndex, accountInfo.ExtPubKey, network, false);

        accountInfo.LastFetchIndex = index;
        foreach (var (address, addressInfo) in items)
        {
            accountInfo.AddressesInfo.Remove(address);
            accountInfo.AddressesInfo.Add(address, addressInfo);
            accountInfo.TotalBalance += addressInfo.Balance;
        }
        
        var (changeIndex, changeItems) = await FetcAddressesDataForPubKeyAsync(accountInfo.LastFetchChangeIndex, accountInfo.ExtPubKey, network, true);

        accountInfo.LastFetchChangeIndex = changeIndex;
        foreach (var (address, changeAddressInfo) in changeItems)
        {
            accountInfo.AddressesInfo.Remove(address);
            accountInfo.AddressesInfo.Add(address, changeAddressInfo);
            accountInfo.TotalBalance += changeAddressInfo.Balance;
        }

        _storage.SetAccountInfo(network.Name, accountInfo);

        return accountInfo;
    }

    private async Task<(int,Dictionary<string,AddressInfo>)> FetcAddressesDataForPubKeyAsync(int scanIndex, string ExtendedPubKey, Network network, bool isChange)
    {
        ExtPubKey accountExtPubKey = ExtPubKey.Parse(ExtendedPubKey, network);
        
        var addressesInfo = new Dictionary<string,AddressInfo>();
        var accountIndex = 0; // for now only account 0
        var purpose = 84; // for now only legacy
        
        var gap = 5;
        while (gap > 0)
        {
            PubKey pubkey = _hdOperations.GeneratePublicKey(accountExtPubKey, scanIndex, isChange);
            var path = _hdOperations.CreateHdPath(purpose, network.Consensus.CoinType, accountIndex, isChange, scanIndex);
            
            var address = pubkey.GetSegwitAddress(network).ToString();
            var result = await FetchUtxoForAddressAsync(address);

            addressesInfo.Add(address,
                new AddressInfo { HdPath = path, UtxoData = result.data, HasHistory = !result.noHistory });
            scanIndex++;

            if (!result.noHistory) continue;
            
            gap--;
        }

        return (scanIndex, addressesInfo);
    }

    public async Task<(bool noHistory, List<UtxoData> data)> FetchUtxoForAddressAsync(string address)
    {
        var limit = 50;
        var offset = 0;
        List<UtxoData> allItems = new();

        var urlBalance = $"/query/address/{address}";
        IndexerUrl indexer = _networkConfiguration.getIndexerUrl();
        var addressBalance = await _http.GetFromJsonAsync<AddressBalance>(indexer.Url + urlBalance);

        if (addressBalance?.balance == 0 && (addressBalance.totalReceivedCount + addressBalance.totalSentCount) == 0)
        {
            return (true, allItems);
        }

        int fetchCount = 50; // for the demo we just scan 50 addresses

        for (int i = 0; i < fetchCount; i++)
        {
            // this is inefficient look at headers to know when to stop

            var url = $"/query/address/{address}/transactions/unspent?confirmations=0&offset={offset}&limit={limit}";

            Console.WriteLine($"fetching {url}");

            var response = await _http.GetAsync(indexer.Url + url);
            var utxo = await response.Content.ReadFromJsonAsync<List<UtxoData>>();

            if (utxo == null || !utxo.Any())
                break;

            allItems.AddRange(utxo);

            offset += limit;
        }

        return (false, allItems);
    }

    public async Task<IEnumerable<FeeEstimation>> GetFeeEstimationAsync()
    {
        var blocks = new []{1,5,10};

        try
        {
            IndexerUrl indexer = _networkConfiguration.getIndexerUrl();
            
            var url = "/stats/fee?" + blocks.Select(_ => $"confirmations={_}");

            Console.WriteLine($"fetching fee estimation for blocks - {blocks}");

            var response = await _http.GetAsync(indexer.Url + url);
            
            var feeEstimations = await response.Content.ReadFromJsonAsync<FeeEstimations>();

            if (feeEstimations == null || (!feeEstimations.Fees?.Any() ?? true))
                return blocks.Select(_ => new FeeEstimation{Confirmations = _,FeeRateet = _ * 100});

            return feeEstimations.Fees;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}
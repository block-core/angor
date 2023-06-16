using System.Net.Http.Json;
using Angor.Client.Shared.Models;
using Angor.Client.Shared.Types;
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
    private readonly IHdOperations _hdOperations;
    private readonly ILogger<WalletOperations> _logger;
    private readonly INetworkConfiguration _networkConfiguration;

    public WalletOperations(HttpClient http, IClientStorage storage, IHdOperations hdOperations, ILogger<WalletOperations> logger, INetworkConfiguration networkConfiguration)
    {
        _http = http;
        _storage = storage;
        _hdOperations = hdOperations;
        _logger = logger;
        _networkConfiguration = networkConfiguration;
    }

    public bool HasWallet()
    {
        return _storage.GetWalletWords() != null;
    }

    public void CreateWallet(WalletWords walletWords)
    {
        if (_storage.GetWalletWords() != null)
        {
            throw new ArgumentNullException("Wallet already exists!");
        }

        var data = walletWords.ConvertToString();

        _storage.SaveWalletWords(data);
    }

    public void DeleteWallet()
    {
        _storage.DeleteWalletWords();
    }

    public WalletWords GetWallet()
    {
        var words = _storage.GetWalletWords();

        if (string.IsNullOrEmpty(words))
        {
            throw new ArgumentNullException("Wallet not found!");

        }

        return WalletWords.ConvertFromString(words);
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
        var coinType = network.Consensus.CoinType;
        var accountIndex = 0; // for now only account 0
        var purpose = 84; // for now only legacy

        AccountInfo accountInfo = _storage.GetAccountInfo(network.Name);

        var utxos = new List<UtxoData>();
        utxos.AddRange(accountInfo.UtxoItems.SelectMany(_ => _.Value));
        utxos.AddRange(accountInfo.UtxoChangeItems.SelectMany(_ => _.Value));

        var utxosToSpend = new List<UtxoData>();

        long ToSendSats = Money.Coins(sendAmount).Satoshi;

        long total = 0;
        foreach (var utxoData in utxos.OrderBy(o => o.blockIndex).ThenByDescending(o => o.value))
        {
            utxosToSpend.Add(utxoData);

            total += utxoData.value;

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
            extendedKey = _hdOperations.GetExtendedKey(_storage.GetWalletWords() ??
                                                       throw new ArgumentNullException("Wallet words not found"));
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

        foreach (var utxoData in utxosToSpend)
        {
            coins.Add(new Coin(uint256.Parse(utxoData.outpoint.transactionId), (uint)utxoData.outpoint.outputIndex,
                Money.Satoshis(utxoData.value), Script.FromHex(utxoData.scriptHex)));

            // derive the private key
            var extKey = extendedKey.Derive(new KeyPath(utxoData.hdPath));
            Key privateKey = extKey.PrivateKey;
            keys.Add(privateKey);
        }

        var change = accountInfo.UtxoChangeItems.First(f => f.Value.Count == 0).Key;

        var builder = new TransactionBuilder(network)
            .Send(BitcoinWitPubKeyAddress.Create(sendToAddress, network), Money.Coins(sendAmount))
            .AddCoins(coins)
            .AddKeys(keys.ToArray())
            .SetChange(BitcoinWitPubKeyAddress.Create(change, network))
            .SendFees(Money.Satoshis(selectedFee));

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
            extendedKey = _hdOperations.GetExtendedKey(_storage.GetWalletWords());
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
        //storage.SetWalletPrivkey(extendedKey.ToString(network)!);
        ExtPubKey accountExtPubKeyTostore =
            _hdOperations.GetExtendedPublicKey(privateKey, extendedKey.ChainCode, accountHdPath);

        accountInfo.ExtPubKey = accountExtPubKeyTostore.ToString(network);
        accountInfo.Path = accountHdPath;

        _storage.SetAccountInfo(network.Name, accountInfo);
    }

    public async Task UpdateAccountInfo()
    {
        ExtKey.UseBCForHMACSHA512 = true;
        Blockcore.NBitcoin.Crypto.Hashes.UseBCForHMACSHA512 = true;

        Network network = _networkConfiguration.GetNetwork();

        AccountInfo accountInfo = _storage.GetAccountInfo(network.Name);

        var (index, items) = await FetchUtxoForAddressAsync(accountInfo.LastFetchIndex, accountInfo.ExtPubKey, network, false);

        accountInfo.LastFetchIndex = index;
        foreach (var (address, utxoData) in items)
        {
            accountInfo.UtxoItems.Remove(address);
            accountInfo.UtxoItems.Add(address, utxoData);
            accountInfo.TotalBalance += utxoData.Sum(s => s.value);
        }
        
        var (changeIndex, changeItems) = await FetchUtxoForAddressAsync(accountInfo.LastFetchChangeIndex, accountInfo.ExtPubKey, network, true);

        accountInfo.LastFetchChangeIndex = changeIndex;
        foreach (var (address, utxoData) in changeItems)
        {
            accountInfo.UtxoChangeItems.Remove(address);
            accountInfo.UtxoChangeItems.Add(address, utxoData);
            accountInfo.TotalBalance += utxoData.Sum(s => s.value);
        }

        _storage.SetAccountInfo(network.Name, accountInfo);
    }

    private async Task CheckExistingAddresses(AccountInfo accountInfo) //TODO make this a public call
    {
        foreach (var item in accountInfo.UtxoItems)
        {
            if (item.Value.Any())
            {
                var result = await FetchUtxos(item.Key);

                if (result.data.Count == item.Value.Count)
                {
                    for (int i = 0; i < result.data.Count - 1; i++)
                    {
                        if (result.data[i].outpoint.transactionId != item.Value[i].outpoint.transactionId)
                        {
                            result.data.ForEach(f => f.hdPath = item.Value[0].hdPath);

                            item.Value.Clear();
                            item.Value.AddRange(result.data);
                            break;
                        }

                    }
                }
                else
                {
                    result.data.ForEach(f => f.hdPath = item.Value[0].hdPath);
                    item.Value.Clear();
                    item.Value.AddRange(result.data);
                }
            }
        }

        foreach (var item in accountInfo.UtxoChangeItems)
        {
            if (item.Value.Any())
            {
                var result = await FetchUtxos(item.Key);

                if (result.data.Count == item.Value.Count)
                {
                    for (int i = 0; i < result.data.Count - 1; i++)
                    {
                        if (result.data[i].outpoint.transactionId != item.Value[i].outpoint.transactionId)
                        {
                            item.Value.Clear();
                            item.Value.AddRange(result.data);
                            break;
                        }

                    }
                }
                else
                {
                    item.Value.Clear();
                    item.Value.AddRange(result.data);
                }
            }
        }
    }
    
    private async Task<(int,Dictionary<string,List<UtxoData>>)> FetchUtxoForAddressAsync(int scanIndex, string ExtendedPubKey, Network network, bool isChange)
    {
        ExtPubKey accountExtPubKey = ExtPubKey.Parse(ExtendedPubKey, network);
        
        var UtxoItems = new Dictionary<string,List<UtxoData>>();
        var accountIndex = 0; // for now only account 0
        var purpose = 84; // for now only legacy
        
        var gap = 5;
        while (gap > 0)
        {
            PubKey pubkey = _hdOperations.GeneratePublicKey(accountExtPubKey, scanIndex, isChange);
            var path = _hdOperations.CreateHdPath(purpose, network.Consensus.CoinType, accountIndex, isChange, scanIndex);
            
            var address = pubkey.GetSegwitAddress(network).ToString();
            var result = await FetchUtxos(address);
            
            foreach (var utxoData in result.data)
                utxoData.hdPath = path;
            
            UtxoItems.Add(address, result.data);
            scanIndex++;

            if (!result.noHistory) continue;
            
            gap--;
        }

        return (scanIndex, UtxoItems);
    }

    private async Task<(int, string, List<UtxoData>)> FetchUtxoForAddressAsync(int scanIndex,
        ExtPubKey accountExtPubKey, Network network, bool isChange)
    {
        var UtxoItems = new Dictionary<string, List<UtxoData>>();
        var accountIndex = 0; // for now only account 0
        var purpose = 84; // for now only legacy

        PubKey pubkey = _hdOperations.GeneratePublicKey(accountExtPubKey, scanIndex, isChange);
        var path = _hdOperations.CreateHdPath(purpose, network.Consensus.CoinType, accountIndex, isChange, scanIndex);

        var address = pubkey.GetSegwitAddress(network).ToString();
        var result = await FetchUtxos(address);

        foreach (var utxoData in result.data)
            utxoData.hdPath = path;

        return (scanIndex++, address, result.data);
    }

    public async Task<(bool noHistory, List<UtxoData> data)> FetchUtxos(string adddress)
    {
        var limit = 50;
        var offset = 0;
        List<UtxoData> allItems = new();

        var urlBalance = $"/query/address/{adddress}";
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

            var url = $"/query/address/{adddress}/transactions/unspent?confirmations=0&offset={offset}&limit={limit}";

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
}
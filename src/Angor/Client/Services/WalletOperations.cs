using Angor.Client.Shared.Models;
using Angor.Shared;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP32;
using Blockcore.Networks;

namespace Angor.Client.Services;

public class WalletOperations
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

    public async Task<(bool, string)> SendAmountToAddress(decimal sendAmount, long selectedFee, string sendToAddress)
    {
        try
        {
            Network network = _networkConfiguration.GetNetwork();
            var coinType = network.Consensus.CoinType;
            var accountIndex = 0; // for now only account 0
            var purpose = 84; // for now only legacy

            AccountInfo accountInfo = _storage.GetAccountInfo(_networkConfiguration.GetNetwork().Name);

            var utxos = new List<UtxoData>();
            utxos.AddRange(accountInfo.UtxoItems.Values.SelectMany(_ => _));
            utxos.AddRange(accountInfo.UtxoChangeItems.Values.SelectMany(_ => _));

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

            var url = Path.Combine(_networkConfiguration.getIndexerUrl().Url, "/command/send");
            
            var res = await _http.PostAsync(url, new StringContent(hex));

            if (res.IsSuccessStatusCode)
                return (true,
                    $"Transaction Sent! - {sendAmount} coins to {sendToAddress} - trxid = {signedTransaction.GetHash()}");
            
            var content = await res.Content.ReadAsStringAsync();
            return (false, res.ReasonPhrase + content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw;
        }
    }
}
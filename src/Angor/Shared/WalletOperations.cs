using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP32;
using Blockcore.NBitcoin.BIP39;
using Blockcore.Networks;
using Microsoft.Extensions.Logging;

namespace Angor.Shared;

public class WalletOperations : IWalletOperations 
{
    private readonly IHdOperations _hdOperations;
    private readonly ILogger<WalletOperations> _logger;
    private readonly INetworkConfiguration _networkConfiguration;
    private readonly IIndexerService _indexerService;

    private const int AccountIndex = 0; // for now only account 0
    private const int Purpose = 84; // for now only legacy

    public WalletOperations(IIndexerService indexerService, IHdOperations hdOperations, ILogger<WalletOperations> logger, INetworkConfiguration networkConfiguration)
    {
        _hdOperations = hdOperations;
        _logger = logger;
        _networkConfiguration = networkConfiguration;
        _indexerService = indexerService;
    }

    public string GenerateWalletWords()
    {
        var count = (WordCount)12;
        var mnemonic = new Mnemonic(Wordlist.English, count);
        string walletWords = mnemonic.ToString();
        return walletWords;
    }
    
    public TransactionInfo AddInputsAndSignTransaction(string changeAddress, Transaction transaction,
        WalletWords walletWords, AccountInfo accountInfo, FeeEstimation feeRate)
    {
        Network network = _networkConfiguration.GetNetwork();

        var utxoDataWithPaths = FindOutputsForTransaction((long)transaction.Outputs.Sum(_ => _.Value), accountInfo);
        var coins = GetUnspentOutputsForTransaction(walletWords, utxoDataWithPaths);

        if (coins.coins == null)
            throw new ApplicationException("No coins found");

        var builder = new TransactionBuilder(network)
            .AddCoins(coins.coins)
            .AddKeys(coins.keys.ToArray())
            .SetChange(BitcoinAddress.Create(changeAddress, network))
            .ContinueToBuild(transaction)
            .SendEstimatedFees(new FeeRate(Money.Satoshis(feeRate.FeeRate)))
            .CoverTheRest();

        var signTransaction = builder.BuildTransaction(true);

        // find the coins used
        long totaInInputs = 0;
        long totaInOutputs = signTransaction.Outputs.Select(s => s.Value.Satoshi).Sum();

        foreach (var input in signTransaction.Inputs)
        {
            var foundInput = coins.coins.First(c => c.Outpoint.ToString() == input.PrevOut.ToString());

            totaInInputs += foundInput.Amount.Satoshi;
        }

        var minerFee = totaInInputs - totaInOutputs;

        return new TransactionInfo { Transaction = signTransaction, TransactionFee = minerFee };
    }

    public TransactionInfo AddFeeAndSignTransaction(string changeAddress, Transaction transaction,
        WalletWords walletWords, AccountInfo accountInfo, FeeEstimation feeRate)
    {
        Network network = _networkConfiguration.GetNetwork();

        var clonedTransaction = network.CreateTransaction(transaction.ToHex());

        var changeOutput = clonedTransaction.AddOutput(Money.Zero, BitcoinAddress.Create(changeAddress, network).ScriptPubKey);

        var virtualSize = clonedTransaction.GetVirtualSize(4);
        var fee = new FeeRate(Money.Satoshis(feeRate.FeeRate)).GetFee(virtualSize);
        
        var utxoDataWithPaths = FindOutputsForTransaction((long)fee, accountInfo);
        var coins = GetUnspentOutputsForTransaction(walletWords, utxoDataWithPaths);

        // todo: dan - the fee here is calculated for the trx size before adding inputs,
        // we must increase the fee to account also for the new inputs that the fee is paid from.

        var totalSats = coins.coins.Sum(s => s.Amount.Satoshi);
        totalSats -= fee;
        changeOutput.Value = new Money(totalSats);

        // add all inputs
        foreach (var coin in coins.coins)
        {
            clonedTransaction.AddInput(new TxIn(coin.Outpoint, null));
        }

        // sign each new input
        var index = 0;
        foreach (var coin in coins.coins)
        {
            var key = coins.keys[index];

            var input = clonedTransaction.Inputs.Single(p => p.PrevOut == coin.Outpoint);
            var signature = clonedTransaction.SignInput(network, key, coin, SigHash.All);
            input.WitScript = new WitScript(Op.GetPushOp(signature.ToBytes()), Op.GetPushOp(key.PubKey.ToBytes()));

            index++;
        }

        return new TransactionInfo { Transaction = clonedTransaction, TransactionFee = fee };
    }

    public async Task<OperationResult<Transaction>> SendAmountToAddress(WalletWords walletWords, SendInfo sendInfo) //TODO change the passing of wallet words as parameter after refactoring is complete
    {
        Network network = _networkConfiguration.GetNetwork();

        if (sendInfo.SendAmountSat > sendInfo.SendUtxos.Values.Sum(s => s.UtxoData.value))
        {
            throw new ApplicationException("not enough funds");
        }
        
        var (coins, keys) =
            GetUnspentOutputsForTransaction(walletWords,sendInfo.SendUtxos.Values.ToList());
        
        if (coins == null)
        {
            return new OperationResult<Transaction> { Success = false, Message = "not enough funds" };
        }

        var builder = new TransactionBuilder(network)
            .Send(BitcoinWitPubKeyAddress.Create(sendInfo.SendToAddress, network), Money.Coins(sendInfo.SendAmount))
            .AddCoins(coins)
            .AddKeys(keys.ToArray())
            .SetChange(BitcoinWitPubKeyAddress.Create(sendInfo.ChangeAddress, network))
            .SendEstimatedFees(new FeeRate(Money.Coins(sendInfo.FeeRate)));

        var signedTransaction = builder.BuildTransaction(true);

        return await PublishTransactionAsync(network, signedTransaction);
    }

    public List<UtxoData> UpdateAccountUnconfirmedInfoWithSpentTransaction(AccountInfo accountInfo, Transaction transaction)
    {
        Network network = _networkConfiguration.GetNetwork();
        
        var inputs = transaction.Inputs.Select(_ => _.PrevOut.ToString()).ToList();

        var accountChangeAddresses = accountInfo.ChangeAddressesInfo.Select(x => x.Address).ToList();
        
        var transactionHash = transaction.GetHash().ToString();

        foreach (var utxoData in accountInfo.AllUtxos())
        {
            // find all spent inputs to mark them as spent
            if (inputs.Contains(utxoData.outpoint.ToString()))
                utxoData.PendingSpent = true;
        }

        List<UtxoData> list = new();

        foreach (var output in transaction.Outputs.AsIndexedOutputs())
        {
            var address = output.TxOut.ScriptPubKey.GetDestinationAddress(network)?.ToString();

            if (address != null && accountChangeAddresses.Contains(address))
            {
                list.Add(new UtxoData
                {
                    address = output.TxOut.ScriptPubKey.GetDestinationAddress(network).ToString(),
                    scriptHex = output.TxOut.ScriptPubKey.ToHex(),
                    outpoint = new Outpoint(transactionHash, (int)output.N),
                    blockIndex = 0,
                    value = output.TxOut.Value
                });
            }
        }

        return list;
    }

    public async Task<OperationResult<Transaction>> PublishTransactionAsync(Network network,Transaction signedTransaction)
    {
        var hex = signedTransaction.ToHex(network.Consensus.ConsensusFactory);

        var res = await _indexerService.PublishTransactionAsync(hex);

        if (string.IsNullOrEmpty(res))
            return new OperationResult<Transaction> { Success = true, Data = signedTransaction };

        return new OperationResult<Transaction> { Success = false, Message = res };
    }

    public List<UtxoDataWithPath> FindOutputsForTransaction(long sendAmountat, AccountInfo accountInfo)
    {
        var utxosToSpend = new List<UtxoDataWithPath>();

        long total = 0;
        foreach (var utxoData in accountInfo.AllAddresses().SelectMany(_ => _.UtxoData
                         .Where(utxow => utxow.PendingSpent == false)
                         .Select(u => new { path = _.HdPath, utxo = u }))
                     .OrderBy(o => o.utxo.blockIndex)
                     .ThenByDescending(o => o.utxo.value))
        {
            if (accountInfo.UtxoReservedForInvestment.Contains(utxoData.utxo.outpoint.ToString()))
                continue;

            utxosToSpend.Add(new UtxoDataWithPath { HdPath = utxoData.path, UtxoData = utxoData.utxo });

            total += utxoData.utxo.value;

            if (total > sendAmountat)
            {
                break;
            }
        }

        if (total < sendAmountat)
        {
            throw new ApplicationException($"Not enough funds, expected {Money.Satoshis(sendAmountat)} BTC, found {Money.Satoshis(total)} BTC");
        }

        return utxosToSpend;
    }

    public (List<Coin>? coins,List<Key> keys) GetUnspentOutputsForTransaction(WalletWords walletWords , List<UtxoDataWithPath> utxoDataWithPaths)
    {
        ExtKey extendedKey;
        try
        {
            extendedKey = _hdOperations.GetExtendedKey(walletWords.Words, walletWords.Passphrase); //TODO change this to be the extended key 
        }
        catch (NotSupportedException ex)
        {
            Console.WriteLine("Exception occurred: {0}", ex);

            if (ex.Message == "Unknown")
                throw new Exception("Please make sure you enter valid mnemonic words.");

            throw;
        }

        var coins = new List<Coin>();
        var keys = new List<Key>();

        foreach (var utxoDataWithPath in utxoDataWithPaths)
        {
            var utxo = utxoDataWithPath.UtxoData;

            coins.Add(new Coin(uint256.Parse(utxo.outpoint.transactionId), (uint)utxo.outpoint.outputIndex,
                Money.Satoshis(utxo.value), Script.FromHex(utxo.scriptHex)));

            // derive the private key
            var extKey = extendedKey.Derive(new KeyPath(utxoDataWithPath.HdPath));
            Key privateKey = extKey.PrivateKey;
            keys.Add(privateKey);
        }

        return (coins,keys);
    }

    public AccountInfo BuildAccountInfoForWalletWords(WalletWords walletWords)
    {
        ExtKey.UseBCForHMACSHA512 = true;
        Blockcore.NBitcoin.Crypto.Hashes.UseBCForHMACSHA512 = true;

        Network network = _networkConfiguration.GetNetwork();
        var coinType = network.Consensus.CoinType;

        var accountInfo = new AccountInfo();

        ExtKey extendedKey;
        try
        {
            extendedKey = _hdOperations.GetExtendedKey(walletWords.Words, walletWords.Passphrase);
        }
        catch (NotSupportedException ex)
        {
            Console.WriteLine("Exception occurred: {0}", ex.ToString());

            if (ex.Message == "Unknown")
                throw new Exception("Please make sure you enter valid mnemonic words.");

            throw;
        }

        string accountHdPath = _hdOperations.GetAccountHdPath(Purpose, coinType, AccountIndex);
        Key privateKey = extendedKey.PrivateKey;

        ExtPubKey accountExtPubKeyTostore =
            _hdOperations.GetExtendedPublicKey(privateKey, extendedKey.ChainCode, accountHdPath);

        accountInfo.ExtPubKey = accountExtPubKeyTostore.ToString(network);
        accountInfo.Path = accountHdPath;
        
        return accountInfo;
    }

    public async Task UpdateDataForExistingAddressesAsync(AccountInfo accountInfo)
    {
        ExtKey.UseBCForHMACSHA512 = true;
        Blockcore.NBitcoin.Crypto.Hashes.UseBCForHMACSHA512 = true;

        var addressTasks=  accountInfo.AddressesInfo.Select(UpdateAddressInfoUtxoData);
        
        var changeAddressTasks=  accountInfo.ChangeAddressesInfo.Select(UpdateAddressInfoUtxoData);

        await Task.WhenAll(addressTasks.Concat(changeAddressTasks));
    }

    private async Task UpdateAddressInfoUtxoData(AddressInfo addressInfo)
    {
        if (!addressInfo.UtxoData.Any() && addressInfo.HasHistory) return;

        var (address, utxoList) = await FetchUtxoForAddressAsync(addressInfo.Address);
        
        if (utxoList.Count != addressInfo.UtxoData.Count 
            || addressInfo.UtxoData.Any(_ => _.blockIndex == 0) 
            || utxoList.Where((_, i) => _.outpoint.transactionId != addressInfo.UtxoData[i].outpoint.transactionId).Any())
        {
            CopyPendingSpentUtxos(addressInfo.UtxoData, utxoList);
            addressInfo.UtxoData.Clear();
            addressInfo.UtxoData.AddRange(utxoList);
        }
    }

    private void CopyPendingSpentUtxos(List<UtxoData> from, List<UtxoData> to)
    {
        foreach (var utxoFrom in from.Where(x => x.PendingSpent))
        {
            var newUtxo = to.FirstOrDefault(x => x.outpoint.ToString() == utxoFrom.outpoint.ToString());
            if (newUtxo != null)
            {
                newUtxo.PendingSpent = true;
            }
        }
    }

    public async Task UpdateAccountInfoWithNewAddressesAsync(AccountInfo accountInfo)
    {
        ExtKey.UseBCForHMACSHA512 = true;
        Blockcore.NBitcoin.Crypto.Hashes.UseBCForHMACSHA512 = true;

        Network network = _networkConfiguration.GetNetwork();
        
        var (index, items) = await FetchAddressesDataForPubKeyAsync(accountInfo.LastFetchIndex, accountInfo.ExtPubKey, network, false);

        accountInfo.LastFetchIndex = index;
        foreach (var addressInfoToAdd in items)
        {
            var addressInfoToDelete = accountInfo.AddressesInfo.SingleOrDefault(_ => _.Address == addressInfoToAdd.Address);
            if (addressInfoToDelete != null)
            {
                // TODO need to update the indexer response with mempool utxo as well so it is always consistant

                CopyPendingSpentUtxos(addressInfoToDelete.UtxoData, addressInfoToAdd.UtxoData);
                accountInfo.AddressesInfo.Remove(addressInfoToDelete);
            }
            
            accountInfo.AddressesInfo.Add(addressInfoToAdd);
        }

        var (changeIndex, changeItems) = await FetchAddressesDataForPubKeyAsync(accountInfo.LastFetchChangeIndex, accountInfo.ExtPubKey, network, true);

        accountInfo.LastFetchChangeIndex = changeIndex;
        foreach (var changeAddressInfoToAdd in changeItems)
        {
            var changeAddressInfoToDelete = accountInfo.ChangeAddressesInfo.SingleOrDefault(_ => _.Address == changeAddressInfoToAdd.Address);
            if (changeAddressInfoToDelete != null)
            {
                // TODO need to update the indexer response with mempool utxo as well so it is always consistant

                CopyPendingSpentUtxos(changeAddressInfoToDelete.UtxoData, changeAddressInfoToAdd.UtxoData);
                accountInfo.ChangeAddressesInfo.Remove(changeAddressInfoToDelete);
            }
            
            accountInfo.ChangeAddressesInfo.Add(changeAddressInfoToAdd);
        }
    }

    private async Task<(int,List<AddressInfo>)> FetchAddressesDataForPubKeyAsync(int scanIndex, string ExtendedPubKey, Network network, bool isChange)
    {
        ExtPubKey accountExtPubKey = ExtPubKey.Parse(ExtendedPubKey, network);
        
        var addressesInfo = new List<AddressInfo>();

        var gap = 5;
        AddressInfo? newEmptyAddress = null;
        AddressBalance[] addressesNotEmpty;
        do
        {
            var newAddressesToCheck = Enumerable.Range(0, gap)
                .Select(_ => GenerateAddressFromPubKey(scanIndex + _, network, isChange, accountExtPubKey))
                .ToList();

            //check all new addresses for balance or a history
            addressesNotEmpty = await _indexerService.GetAdressBalancesAsync(newAddressesToCheck, true);

            if (addressesNotEmpty.Length < newAddressesToCheck.Count)
                newEmptyAddress = newAddressesToCheck[addressesNotEmpty.Length];

            if (!addressesNotEmpty.Any())
                break; //No new data for the addresses checked
            
            //Add the addresses with balance or a history to the returned list
            addressesInfo.AddRange(newAddressesToCheck
                .Where(addressInfo => addressesNotEmpty
                    .Any(_ => _.address == addressInfo.Address)));

            var tasks = addressesNotEmpty.Select(_ => FetchUtxoForAddressAsync(_.address));

            var lookupResults = await Task.WhenAll(tasks);

            foreach (var (address, data) in lookupResults)
            {
                var addressInfo = addressesInfo.First(_ => _.Address == address);
                addressInfo.HasHistory = true;
                addressInfo.UtxoData = data;
            }

            scanIndex += addressesNotEmpty.Length;

        } while (addressesNotEmpty.Any());

        if (newEmptyAddress != null) //empty address for receiving funds
            addressesInfo.Add(newEmptyAddress);
        
        return (scanIndex, addressesInfo);
    }

    private AddressInfo GenerateAddressFromPubKey(int scanIndex, Network network, bool isChange, ExtPubKey accountExtPubKey)
    {
        var pubKey = _hdOperations.GeneratePublicKey(accountExtPubKey, scanIndex, isChange);
        var path = _hdOperations.CreateHdPath(Purpose, network.Consensus.CoinType, AccountIndex, isChange, scanIndex);
        var address = pubKey.GetSegwitAddress(network).ToString();

        return new AddressInfo { Address = address, HdPath = path };
    }

    public async Task<(string address, List<UtxoData> data)> FetchUtxoForAddressAsync(string address)
    {
        // cap utxo count to max 1000 items, this is
        // mainly to get miner wallets to work fine
        var maxutxo = 200; 

        var limit = 50;
        var offset = 0;
        List<UtxoData> allItems = new();
        
        do
        {
            // this is inefficient look at headers to know when to stop
            var utxo = await _indexerService.FetchUtxoAsync(address, offset, limit);

            if (utxo == null || !utxo.Any())
                break;

            allItems.AddRange(utxo);

            if (utxo.Count < limit)
                break;

            if (allItems.Count >= maxutxo)
            {
                _logger.LogWarning($"utxo scan for address {address} was stopped after the limit of {maxutxo} was reached.");
                break;
            }

            offset += limit;
        } while (true);

        // todo: dan - this is a hack until the endpoint offset is fixed
        allItems = allItems.DistinctBy(d => d.outpoint.ToString()).ToList();

        return (address, allItems);
    }

    public async Task<IEnumerable<FeeEstimation>> GetFeeEstimationAsync()
    {
        var blocks = new []{1,5,10};

        try
        {
            _logger.LogInformation($"fetching fee estimation for blocks");

            var feeEstimations = await _indexerService.GetFeeEstimationAsync(blocks);

            if (feeEstimations == null || (!feeEstimations.Fees?.Any() ?? true))
                return blocks.Select(_ => new FeeEstimation{Confirmations = _,FeeRate = 10000 / _});

            return feeEstimations.Fees!;
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message, e);
            throw;
        }
    }

    public decimal CalculateTransactionFee(SendInfo sendInfo, AccountInfo accountInfo, long feeRate)
    {
        var network = _networkConfiguration.GetNetwork();

        if (sendInfo.SendUtxos.Count == 0)
        {
            var utxosToSpend = FindOutputsForTransaction(sendInfo.SendAmountSat, accountInfo);

            foreach (var data in utxosToSpend) //TODO move this out of the fee calculation
            {
                sendInfo.SendUtxos.Add(data.UtxoData.outpoint.ToString(), data);
            }
        }

        var coins = sendInfo.SendUtxos
            .Select(_ => _.Value.UtxoData)
            .Select(_ => new Coin(uint256.Parse(_.outpoint.transactionId), (uint)_.outpoint.outputIndex,
                Money.Satoshis(_.value), Script.FromHex(_.scriptHex)));

        var builder = new TransactionBuilder(network)
            .Send(BitcoinWitPubKeyAddress.Create(sendInfo.SendToAddress, network), sendInfo.SendAmountSat)
            .AddCoins(coins)
            .SetChange(BitcoinWitPubKeyAddress.Create(sendInfo.ChangeAddress, network));

        return builder.EstimateFees(new FeeRate(Money.Satoshis(feeRate))).ToUnit(MoneyUnit.BTC);
    }
}
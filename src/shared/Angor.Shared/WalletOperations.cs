using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Angor.Shared.Utilities;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Crypto;

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
        WalletWords walletWords, AccountInfo accountInfo, long feeRate)
    {
        AngorNetwork network = _networkConfiguration.GetNetwork();

        var utxoDataWithPaths = FindOutputsForTransaction(transaction.Outputs.Sum(_ => _.Value.Satoshi), accountInfo);
        var signingCoins = GetUnspentOutputsForTransaction(walletWords, utxoDataWithPaths);

        if (signingCoins == null || !signingCoins.Any())
            throw new ApplicationException("No coins found");
       
        // did we spend all the coins?
        var spendAll = signingCoins.Sum(s => s.Coin.Amount.Satoshi) == transaction.Outputs.Sum(o => o.Value.Satoshi);

        if (spendAll)
        {
            // Step 1: Clone transaction for modification
            var clonedTransaction = network.CreateTransaction(transaction.ToHex());

            // Step 2: Add inputs and recalculate the transaction size
            foreach (var sc in signingCoins)
            {
                if (clonedTransaction.Inputs.Any(x => x.PrevOut == sc.Coin.Outpoint))
                    continue;
                var txin = new TxIn(sc.Coin.Outpoint);
                txin.WitScript = new WitScript(Op.GetPushOp(new byte[72]), Op.GetPushOp(new byte[33])); // for total size calculation
                clonedTransaction.Inputs.Add(txin);
            }

            // Step 3: Calculate fee, based on the size with inputs
            var totalSize = clonedTransaction.GetVirtualSize();
            long totalFee = new FeeRate(Money.Satoshis(feeRate)).GetFee(totalSize).Satoshi;

            // Step 4: Select the last output to remove the fee from
            var lastOutput = clonedTransaction.Outputs.Last();

            if (totalFee >= lastOutput.Value.Satoshi)
                throw new ApplicationException($"The fee {totalFee} is greater then the last output {lastOutput.Value}");

            // Step 5: remove the fee from the last output
            lastOutput.Value -= Money.Satoshis(totalFee);

            // Step 6: Sign inputs
            foreach (var sc in signingCoins)
            {
                if (sc.Key.PubKey.WitHash.ScriptPubKey != sc.Coin.ScriptPubKey)
                    throw new InvalidOperationException($"Derived key does not match coin ScriptPubKey for outpoint {sc.Coin.Outpoint}");

                var inputIndex = FindInputIndex(clonedTransaction, sc.Coin.Outpoint);
                var scriptCode = sc.Key.PubKey.Hash.ScriptPubKey;
                var sighash = clonedTransaction.GetSignatureHash(scriptCode, inputIndex, SigHash.All, sc.Coin.TxOut, HashVersion.WitnessV0);
                var signature = new TransactionSignature(sc.Key.Sign(sighash), SigHash.All);
                clonedTransaction.Inputs[inputIndex].WitScript = new WitScript(Op.GetPushOp(signature.ToBytes()), Op.GetPushOp(sc.Key.PubKey.ToBytes()));
            }

            return new TransactionInfo { Transaction = clonedTransaction, TransactionFee = totalFee };
        }
        else
        {
            var outputsTotal = transaction.Outputs.Sum(o => o.Value.Satoshi);

            TransactionBuilder NewBuilder(List<SigningCoin> coins)
            {
                var b = network.BitcoinNetwork.CreateTransactionBuilder()
                    .AddCoins(coins.Select(sc => sc.Coin))
                    .AddKeys(coins.Select(sc => sc.Key).ToArray())
                    .SetChange(BitcoinAddress.Create(changeAddress, network.BitcoinNetwork));

                b.ShuffleOutputs = false;
                // Dust prevention is off because the OP_RETURN output carries Money.Zero and
                // NBitcoin would drop it. Sub-dust change is handled explicitly below instead.
                b.DustPrevention = false;

                foreach (var output in transaction.Outputs)
                {
                    b.Send(output.ScriptPubKey, output.Value);
                }

                return b;
            }

            long SumInputs(Transaction tx, List<SigningCoin> coins) =>
                tx.Inputs.Sum(input =>
                    coins.First(sc => sc.Coin.Outpoint.ToString() == input.PrevOut.ToString()).Coin.Amount.Satoshi);

            // FindOutputsForTransaction above only selects enough to cover the outputs, leaving
            // nothing for the miner fee and often producing a sub-dust change output that relay
            // nodes reject with "dust". Size the fee, then re-select coins to cover
            // outputs + fee (+ dust headroom). Extra inputs grow the tx, so iterate.
            const int maxAttempts = 5;

            for (var attempt = 0; ; attempt++)
            {
                // Pass 1: estimate the fee from a fully built (and therefore correctly sized) tx.
                var sizingBuilder = NewBuilder(signingCoins);
                sizingBuilder.SendEstimatedFees(new FeeRate(Money.Satoshis(feeRate)));
                var sizingTransaction = sizingBuilder.BuildTransaction(true);

                var txSize = sizingTransaction.GetVirtualSize();
                var minimumFee = new FeeRate(Money.Satoshis(ProtocolConstants.MinFeeRateSatsPerKb)).GetFee(txSize).Satoshi;
                var estimatedFee = SumInputs(sizingTransaction, signingCoins)
                                   - sizingTransaction.Outputs.Sum(o => o.Value.Satoshi);

                // Guard against the builder under-paying relative to the protocol minimum.
                var totalFee = Math.Max(estimatedFee, minimumFee);
                var totalAvailable = signingCoins.Sum(sc => sc.Coin.Amount.Satoshi);
                var changeAmount = totalAvailable - outputsTotal - totalFee;

                if (changeAmount < 0)
                {
                    if (attempt >= maxAttempts)
                        throw new ApplicationException(
                            $"Not enough funds, expected {(outputsTotal + totalFee).ToUnitBtc()} BTC, found {totalAvailable.ToUnitBtc()} BTC");

                    // Re-select including the fee and dust headroom, then re-measure.
                    var utxosWithFee = FindOutputsForTransaction(
                        outputsTotal + totalFee + ProtocolConstants.DustThresholdSats, accountInfo);
                    signingCoins = GetUnspentOutputsForTransaction(walletWords, utxosWithFee);
                    continue;
                }

                if (changeAmount > 0 && changeAmount <= ProtocolConstants.DustThresholdSats)
                {
                    // Change is dust — give it to the miner instead of creating an output that
                    // gets rejected with "dust". The tx shrinks, so the effective fee rate only rises.
                    totalFee += changeAmount;
                }

                // Pass 2: rebuild with the exact fee. A fresh builder is required because
                // SendFees/SendEstimatedFees accumulate on the same instance.
                // The builder may pick a different coin subset than projected, so verify the
                // built transaction and fold any remaining dust change into the fee.
                var changeScript = BitcoinAddress.Create(changeAddress, network.BitcoinNetwork).ScriptPubKey;
                Transaction signTransaction;
                long actualFee;

                for (var fixup = 0; ; fixup++)
                {
                    var finalBuilder = NewBuilder(signingCoins);
                    finalBuilder.SendFees(Money.Satoshis(totalFee));
                    signTransaction = finalBuilder.BuildTransaction(true);

                    actualFee = SumInputs(signTransaction, signingCoins)
                                - signTransaction.Outputs.Sum(o => o.Value.Satoshi);

                    var actualChange = signTransaction.Outputs
                        .Where(o => o.ScriptPubKey == changeScript)
                        .Sum(o => o.Value.Satoshi);

                    if (actualChange == 0 || actualChange > ProtocolConstants.DustThresholdSats)
                        break;

                    if (fixup >= maxAttempts)
                        throw new ApplicationException(
                            $"Unable to build a transaction without a dust change output ({actualChange} sats)");

                    _logger.LogDebug(
                        "Change output of {Change} sats is below the dust threshold, adding it to the fee", actualChange);
                    totalFee += actualChange;
                }

                return new TransactionInfo { Transaction = signTransaction, TransactionFee = actualFee };
            }
        }
    }

    public TransactionInfo AddInputsFromAddressAndSignTransaction(string fundingAddress, string changeAddress,
        Transaction transaction, WalletWords walletWords, AccountInfo accountInfo, long feeRate)
    {
        AngorNetwork network = _networkConfiguration.GetNetwork();

        // Find UTXOs only for the specified funding address
        var addressInfo = accountInfo.AllAddresses()
            .FirstOrDefault(a => a.Address == fundingAddress);

        if (addressInfo == null)
            throw new ApplicationException($"Address {fundingAddress} not found in account");

        var availableUtxos = addressInfo.UtxoData
            .Where(u => !u.PendingSpent && 
                        !accountInfo.UtxoReservedForInvestment.Contains(u.outpoint.ToString()))
            .Select(u => new UtxoDataWithPath { HdPath = addressInfo.HdPath, UtxoData = u })
            .ToList();

        if (!availableUtxos.Any())
            throw new ApplicationException($"No available UTXOs found for address {fundingAddress}");

        // Calculate required amount (outputs + estimated fee)
        long outputsTotal = transaction.Outputs.Sum(_ => _.Value.Satoshi);
        var estimatedSize = transaction.GetVirtualSize() + (availableUtxos.Count * 68); // Estimate with inputs
        long estimatedFee = new FeeRate(Money.Satoshis(feeRate)).GetFee(estimatedSize).Satoshi;
        long requiredAmount = outputsTotal + estimatedFee;

        // Select UTXOs from the specific address (use all available)
        long totalAvailable = availableUtxos.Sum(u => u.UtxoData.value);

        if (totalAvailable < requiredAmount)
            throw new ApplicationException(
                $"Insufficient funds in address {fundingAddress}. Required: {requiredAmount} sats ({Money.Satoshis(requiredAmount).ToUnit(MoneyUnit.BTC):F8} BTC), Available: {totalAvailable} sats ({Money.Satoshis(totalAvailable).ToUnit(MoneyUnit.BTC):F8} BTC)");

        var signingCoins = GetUnspentOutputsForTransaction(walletWords, availableUtxos);

        if (signingCoins == null || !signingCoins.Any())
            throw new ApplicationException("Failed to get coins for the funding address");

        // Clone transaction
        var clonedTransaction = network.CreateTransaction(transaction.ToHex());

        // Add inputs
        foreach (var sc in signingCoins)
        {
            if (clonedTransaction.Inputs.Any(x => x.PrevOut == sc.Coin.Outpoint))
                continue;
            var txin = new TxIn(sc.Coin.Outpoint);
            txin.WitScript = new WitScript(Op.GetPushOp(new byte[72]), Op.GetPushOp(new byte[33]));
            clonedTransaction.Inputs.Add(txin);
        }

        // Add the change output BEFORE measuring the size, so the fee covers it.
        // (The value is filled in after the fee is known.)
        var changeOutput = new TxOut(Money.Satoshis(0),
            BitcoinAddress.Create(changeAddress, network.BitcoinNetwork).ScriptPubKey);
        clonedTransaction.Outputs.Add(changeOutput);

        // Calculate actual fee
        var totalSize = clonedTransaction.GetVirtualSize();
        long totalFee = new FeeRate(Money.Satoshis(feeRate)).GetFee(totalSize).Satoshi;

        // Calculate change
        var changeAmount = totalAvailable - outputsTotal - totalFee;

        if (changeAmount > ProtocolConstants.DustThresholdSats)
        {
            changeOutput.Value = Money.Satoshis(changeAmount);
        }
        else if (changeAmount >= 0)
        {
            // Change is dust — drop the output and add the remainder to the fee.
            // The tx gets smaller than measured, so the fee rate only goes up.
            clonedTransaction.Outputs.Remove(changeOutput);
            totalFee += changeAmount;
        }
        else
        {
            throw new ApplicationException($"Insufficient funds after fee calculation. Short by {Math.Abs(changeAmount)} sats");
        }

        // Sign inputs
        foreach (var sc in signingCoins)
        {
            if (sc.Key.PubKey.WitHash.ScriptPubKey != sc.Coin.ScriptPubKey)
                throw new InvalidOperationException($"Derived key does not match coin ScriptPubKey for outpoint {sc.Coin.Outpoint}");

            var inputIndex = FindInputIndex(clonedTransaction, sc.Coin.Outpoint);
            var scriptCode = sc.Key.PubKey.Hash.ScriptPubKey;
            var sighash = clonedTransaction.GetSignatureHash(scriptCode, inputIndex, SigHash.All, sc.Coin.TxOut, HashVersion.WitnessV0);
            var signature = new TransactionSignature(sc.Key.Sign(sighash), SigHash.All);
            clonedTransaction.Inputs[inputIndex].WitScript = new WitScript(Op.GetPushOp(signature.ToBytes()), Op.GetPushOp(sc.Key.PubKey.ToBytes()));
        }

        return new TransactionInfo { Transaction = clonedTransaction, TransactionFee = totalFee };
    }

    public TransactionInfo AddFeeAndSignTransaction(string changeAddress, Transaction transaction,
        WalletWords walletWords, AccountInfo accountInfo, long feeRate)
    {
        AngorNetwork network = _networkConfiguration.GetNetwork();

        // Clone transaction for modification
        var clonedTransaction = network.CreateTransaction(transaction.ToHex());
        var changeOutput = new TxOut(Money.Zero, BitcoinAddress.Create(changeAddress, network.BitcoinNetwork).ScriptPubKey);
        clonedTransaction.Outputs.Add(changeOutput);

        // Step 1: Estimate fee for the transaction WITHOUT inputs
        var initialSize = clonedTransaction.GetVirtualSize();
        long initialFee = new FeeRate(Money.Satoshis(feeRate)).GetFee(initialSize).Satoshi;

        // Step 2: Find UTXOs to fund the total cost of transaction (outputs + initial fee)
        var utxoDataWithPaths = FindOutputsForTransaction(initialFee, accountInfo);
        var signingCoins = GetUnspentOutputsForTransaction(walletWords, utxoDataWithPaths);

        // Step 3: Add inputs and recalculate the transaction size
        foreach (var sc in signingCoins)
        {
            if (clonedTransaction.Inputs.Any(x => x.PrevOut == sc.Coin.Outpoint))
                continue;
            var txin = new TxIn(sc.Coin.Outpoint);
            txin.WitScript = new WitScript(Op.GetPushOp(new byte[72]), Op.GetPushOp(new byte[33])); // for total size calculation
            clonedTransaction.Inputs.Add(txin);
        }

        var totalSize = clonedTransaction.GetVirtualSize();

        // Step 4: Calculate fee again, based on the UPDATED size with inputs
        long totalFee = new FeeRate(Money.Satoshis(feeRate)).GetFee(totalSize).Satoshi;

        // Step 5: Adjust the change output (remaining coins after paying the fee)
        var totalSats = signingCoins.Sum(s => s.Coin.Amount.Satoshi);
        totalSats -= totalFee;

        // Handle cases where change is below "dust threshold" for SegWit
        if (totalSats <= ProtocolConstants.DustThresholdSats)
        {
            // Absorb small change into the transaction fee
            changeOutput.Value = Money.Zero;
            totalFee += totalSats; // Add leftover to fee
        }
        else
        {
            changeOutput.Value = Money.Satoshis(totalSats);
        }

        // Step 6: Sign inputs
        foreach (var sc in signingCoins)
        {
            if (sc.Key.PubKey.WitHash.ScriptPubKey != sc.Coin.ScriptPubKey)
                throw new InvalidOperationException($"Derived key does not match coin ScriptPubKey for outpoint {sc.Coin.Outpoint}");

            var inputIndex = FindInputIndex(clonedTransaction, sc.Coin.Outpoint);
            var scriptCode = sc.Key.PubKey.Hash.ScriptPubKey;
            var sighash = clonedTransaction.GetSignatureHash(scriptCode, inputIndex, SigHash.All, sc.Coin.TxOut, HashVersion.WitnessV0);
            var signature = new TransactionSignature(sc.Key.Sign(sighash), SigHash.All);
            clonedTransaction.Inputs[inputIndex].WitScript = new WitScript(Op.GetPushOp(signature.ToBytes()), Op.GetPushOp(sc.Key.PubKey.ToBytes()));
        }

        return new TransactionInfo { Transaction = clonedTransaction, TransactionFee = totalFee };
    }

    public async Task<OperationResult<Transaction>>
        SendAmountToAddress(WalletWords walletWords,
            SendInfo sendInfo) //TODO change the passing of wallet words as parameter after refactoring is complete
    {
        AngorNetwork network = _networkConfiguration.GetNetwork();

        if (sendInfo.SendAmount > sendInfo.SendUtxos.Values.Sum(s => s.UtxoData.value))
        {
            throw new ApplicationException("not enough funds");
        }

        var signingCoins =
            GetUnspentOutputsForTransaction(walletWords, sendInfo.SendUtxos.Values.ToList());

        if (signingCoins == null || !signingCoins.Any())
        {
            return new OperationResult<Transaction> { Success = false, Message = "not enough funds" };
        }

        var builder = network.BitcoinNetwork.CreateTransactionBuilder()
            .Send(BitcoinAddress.Create(sendInfo.SendToAddress, network.BitcoinNetwork), Money.Satoshis(sendInfo.SendAmount))
            .AddCoins(signingCoins.Select(sc => sc.Coin))
            .AddKeys(signingCoins.Select(sc => sc.Key).ToArray())
            .SetChange(BitcoinAddress.Create(sendInfo.ChangeAddress, network.BitcoinNetwork))
            .SendEstimatedFees(new FeeRate(Money.Satoshis(sendInfo.FeeRate)));

        builder.ShuffleOutputs = false;

        var signedTransaction = builder.BuildTransaction(true);

        var hex = signedTransaction.ToHex();
        var res = await _indexerService.PublishTransactionAsync(hex);

        if (string.IsNullOrEmpty(res))
            return new OperationResult<Transaction> { Success = true, Data = signedTransaction };

        return new OperationResult<Transaction> { Success = false, Message = res };
    }

    public async Task<OperationResult<Transaction>> SendAllToAddress(WalletWords walletWords, SendInfo sendInfo)
    {
        AngorNetwork network = _networkConfiguration.GetNetwork();
        List<SigningCoin> signingCoins = GetUnspentOutputsForTransaction(
            walletWords, sendInfo.SendUtxos.Values.ToList());

        if (signingCoins.Count == 0)
            return new OperationResult<Transaction> { Success = false, Message = "not enough funds" };

        var builder = network.BitcoinNetwork.CreateTransactionBuilder()
            .AddCoins(signingCoins.Select(sc => sc.Coin))
            .AddKeys(signingCoins.Select(sc => sc.Key).ToArray())
            .SendAll(BitcoinAddress.Create(sendInfo.SendToAddress, network.BitcoinNetwork))
            .SendEstimatedFees(new FeeRate(Money.Satoshis(sendInfo.FeeRate)));

        builder.ShuffleOutputs = false;

        Transaction signedTransaction = builder.BuildTransaction(true);
        if (signedTransaction.Outputs.Count != 1 || signedTransaction.Outputs[0].Value <= Money.Zero)
            return new OperationResult<Transaction> { Success = false, Message = "not enough funds to cover the network fee" };

        string publishError = await _indexerService.PublishTransactionAsync(signedTransaction.ToHex());
        return string.IsNullOrEmpty(publishError)
            ? new OperationResult<Transaction> { Success = true, Data = signedTransaction }
            : new OperationResult<Transaction> { Success = false, Message = publishError };
    }

    public List<UtxoData> UpdateAccountUnconfirmedInfoWithSpentTransaction(AccountInfo accountInfo, Transaction transaction)
    {
        AngorNetwork network = _networkConfiguration.GetNetwork();
        
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
            var address = output.TxOut.ScriptPubKey.GetDestinationAddress(network.BitcoinNetwork)?.ToString();

            if (address != null && accountChangeAddresses.Contains(address))
            {
                list.Add(new UtxoData
                {
                    address = output.TxOut.ScriptPubKey.GetDestinationAddress(network.BitcoinNetwork).ToString(),
                    scriptHex = output.TxOut.ScriptPubKey.ToHex(),
                    outpoint = new Outpoint(transactionHash, (int)output.N),
                    blockIndex = 0,
                    value = output.TxOut.Value.Satoshi
                });
            }
        }

        return list;
    }

    public List<UtxoDataWithPath> 
        FindOutputsForTransaction(long sendAmountat, AccountInfo accountInfo)
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

            if (total >= sendAmountat)
            {
                break;
            }
        }

        if (total < sendAmountat)
        {
            throw new ApplicationException($"Not enough funds, expected {sendAmountat.ToUnitBtc()} BTC, found {total.ToUnitBtc()} BTC");
        }

        return utxosToSpend;
    }

    public List<SigningCoin> GetUnspentOutputsForTransaction(WalletWords walletWords , List<UtxoDataWithPath> utxoDataWithPaths)
    {
        ExtKey extendedKey;
        try
        {
            extendedKey = walletWords.GetOrDeriveExtKey(_hdOperations);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Failed to derive extended key from mnemonic");

            if (ex.Message == "Unknown")
                throw new Exception("Please make sure you enter valid mnemonic words.");

            throw;
        }

        var signingCoins = new List<SigningCoin>();

        foreach (var utxoDataWithPath in utxoDataWithPaths)
        {
            var utxo = utxoDataWithPath.UtxoData;

            var coin = new Coin(uint256.Parse(utxo.outpoint.transactionId), (uint)utxo.outpoint.outputIndex,
                Money.Satoshis(utxo.value), Script.FromHex(utxo.scriptHex));

            // derive the private key
            var extKey = extendedKey.Derive(new KeyPath(utxoDataWithPath.HdPath));
            Key privateKey = extKey.PrivateKey;
            
            signingCoins.Add(new SigningCoin(coin, privateKey));
        }

        return signingCoins;
    }


    public string DerivePublicKey(WalletWords walletWords, string hdPath)
        => _hdOperations.DerivePublicKey(walletWords.GetOrDeriveExtKey(_hdOperations), hdPath);

    public AngorKey DerivePrivateKey(WalletWords walletWords, string hdPath)
        => _hdOperations.DerivePrivateKey(walletWords.GetOrDeriveExtKey(_hdOperations), hdPath);

    public AccountInfo BuildAccountInfoForWalletWords(WalletWords walletWords)
    {
        AngorNetwork network = _networkConfiguration.GetNetwork();
        var coinType = network.CoinType;

        ExtKey extendedKey;
        try
        {
            extendedKey = walletWords.GetOrDeriveExtKey(_hdOperations);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Failed to derive extended key from mnemonic");

            if (ex.Message == "Unknown")
                throw new Exception("Please make sure you enter valid mnemonic words.");

            throw;
        }

        string accountHdPath = _hdOperations.GetAccountHdPath(Purpose, coinType, AccountIndex);
        Key privateKey = extendedKey.PrivateKey;

        ExtPubKey accountExtPubKeyTostore =
            _hdOperations.GetExtendedPublicKey(privateKey, extendedKey.ChainCode, accountHdPath);

        var rootExtPubKey = extendedKey.Neuter();

        var rootExtPubKeyHash = Convert.ToHexString(Hashes.SHA256(rootExtPubKey.ToBytes()));

        return new AccountInfo()
        {
            walletId = rootExtPubKeyHash,
            RootExtPubKey = rootExtPubKey.ToString(network.BitcoinNetwork),
            ExtPubKey = accountExtPubKeyTostore.ToString(network.BitcoinNetwork),
            Path = accountHdPath
        };
    }

    public async Task UpdateDataForExistingAddressesAsync(AccountInfo accountInfo)
    {
        var addressTasks=  accountInfo.AddressesInfo.Select(UpdateAddressInfoUtxoData);
        
        var changeAddressTasks=  accountInfo.ChangeAddressesInfo.Select(UpdateAddressInfoUtxoData);

        await Task.WhenAll(addressTasks.Concat(changeAddressTasks));
    }

    private async Task UpdateAddressInfoUtxoData(AddressInfo addressInfo)
    {
        if (!addressInfo.UtxoData.Any() && addressInfo.HasHistory)
        {
            _logger.LogInformation($"{addressInfo.Address} has history but no utxo was found");
            return;
        }

        var (address, utxoList) = await FetchUtxoForAddressAsync(addressInfo.Address);
        
        if (utxoList.Count != addressInfo.UtxoData.Count 
            || addressInfo.UtxoData.Any(_ => _.blockIndex == 0) 
            || utxoList.Where((_, i) => _.outpoint.transactionId != addressInfo.UtxoData[i].outpoint.transactionId).Any())
        {
            _logger.LogInformation($"{addressInfo.Address} new utxos found");

            CopyPendingSpentUtxos(addressInfo.UtxoData, utxoList);
            addressInfo.UtxoData.Clear();
            addressInfo.UtxoData.AddRange(utxoList);
        }
        else
        {
            _logger.LogInformation($"{addressInfo.Address} no new utxo found");
        }
    }

    private void CopyPendingSpentUtxos(List<UtxoData> from, List<UtxoData> to)
    {
        foreach (var utxoFrom in from)
        {
            _logger.LogInformation($"{utxoFrom.address} new utxo {utxoFrom.outpoint.ToString()}");

            if (utxoFrom.PendingSpent)
            {
                _logger.LogInformation($"{utxoFrom.address} searching for pending spent utxo for address");

                var newUtxo = to.FirstOrDefault(x => x.outpoint.ToString() == utxoFrom.outpoint.ToString());
                if (newUtxo != null)
                {
                    _logger.LogInformation($"{utxoFrom.address} copying pending spent utxo for address for utxo {newUtxo.outpoint.ToString()}.");
                    newUtxo.PendingSpent = true;
                }
            }
        }
    }

    public async Task UpdateAccountInfoWithNewAddressesAsync(AccountInfo accountInfo)
    {
        AngorNetwork network = _networkConfiguration.GetNetwork();
        
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

    private async Task<(int,List<AddressInfo>)> FetchAddressesDataForPubKeyAsync(int scanIndex, string ExtendedPubKey, AngorNetwork network, bool isChange)
    {
        ExtPubKey accountExtPubKey = ExtPubKey.Parse(ExtendedPubKey, network.BitcoinNetwork);
        
        var addressesInfo = new List<AddressInfo>();

        var gap = 5;
        AddressInfo? newEmptyAddress = null;
        AddressBalance[] addressesNotEmpty;
        do
        {
            _logger.LogInformation($"fetching balance for account = {accountExtPubKey.ToString(network.BitcoinNetwork)} start index = {scanIndex} isChange = {isChange} gap = {gap}");

            var newAddressesToCheck = Enumerable.Range(0, gap)
                .Select(_ => GenerateAddressFromPubKey(scanIndex + _, network, isChange, accountExtPubKey))
            .ToList();

            //check all new addresses for balance or a history
            addressesNotEmpty = await _indexerService.GetAdressBalancesAsync(newAddressesToCheck, true);

            if (addressesNotEmpty.Length < newAddressesToCheck.Count)
                newEmptyAddress = newAddressesToCheck[addressesNotEmpty.Length];

            foreach (var addressInfo in newAddressesToCheck)
            {
                // just for logging
                var foundBalance = addressesNotEmpty.FirstOrDefault(f => f.address == addressInfo.Address);
                _logger.LogInformation($"{addressInfo.Address} balance = {foundBalance?.balance} pending = {foundBalance?.pendingReceived} ");
            }

            if (!addressesNotEmpty.Any())
            {
                _logger.LogInformation($"no new address with balance found");
                break; //No new data for the addresses checked
            }

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

                _logger.LogInformation($"{addressInfo.Address} added utxo data, utxo count = {addressInfo.UtxoData.Count}");
            }

            scanIndex += addressesNotEmpty.Length;

        } while (addressesNotEmpty.Any());

        if (newEmptyAddress != null) //empty address for receiving funds
            addressesInfo.Add(newEmptyAddress);
        
        return (scanIndex, addressesInfo);
    }

    private AddressInfo GenerateAddressFromPubKey(int scanIndex, AngorNetwork network, bool isChange, ExtPubKey accountExtPubKey)
    {
        var pubKey = _hdOperations.GeneratePublicKey(accountExtPubKey, scanIndex, isChange);
        var path = _hdOperations.CreateHdPath(Purpose, network.CoinType, AccountIndex, isChange, scanIndex);
        var address = pubKey.GetAddress(ScriptPubKeyType.Segwit, network.BitcoinNetwork).ToString();

        return new AddressInfo { Address = address, HdPath = path };
    }

    public async Task<(string address, List<UtxoData> data)> FetchUtxoForAddressAsync(string address)
    {
        // cap utxo count to maxutxo items, this is
        // mainly to get miner wallets to work fine
        var maxutxo = 200; 

        var limit = 50;
        var offset = 0;
        List<UtxoData> allItems = new();
        
        do
        {
            _logger.LogInformation($"{address} fetching utxo offset = {offset} limit = {limit}");

            // this is inefficient look at headers to know when to stop
            var utxo = await _indexerService.FetchUtxoAsync(address, offset, limit);

            if (utxo == null || !utxo.Any())
            {
                _logger.LogInformation($"{address} no more utxos found");
                break;
            }

            _logger.LogInformation($"{address} found {utxo.Count} utxos");

            allItems.AddRange(utxo);

            if (utxo.Count < limit)
            {
                _logger.LogInformation($"{address} utxo count {utxo.Count} is under limit {limit} no more utxos to fetch");
                break;
            }

            if (allItems.Count >= maxutxo)
            {
                _logger.LogInformation($"{address} total utxo count {allItems.Count} is greater then max of max {maxutxo} utxos, stopping to fetch utxos");
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
                return blocks.Select(_ => new FeeEstimation{Confirmations = _,FeeRate = 10000 / _}); // default to 1 satoshi per byte for 10 blocks and 10 satoshi for 1 block  

            _logger.LogInformation($"fee estimation is {string.Join(", ", feeEstimations.Fees.Select(f => f.Confirmations.ToString() + "-" + f.FeeRate))}");

            return feeEstimations.Fees!;
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message, e);
            throw;
        }
    }

    public Transaction CreateSendTransaction(SendInfo sendInfo, AccountInfo accountInfo)
    {
        var network = _networkConfiguration.GetNetwork();

        if (sendInfo.SendUtxos.Count == 0)
        {
            var utxosToSpend = FindOutputsForTransaction(sendInfo.SendAmount, accountInfo);

            foreach (var data in utxosToSpend) //TODO move this out of the fee calculation
            {
                sendInfo.SendUtxos.Add(data.UtxoData.outpoint.ToString(), data);
            }
        }

        Transaction transaction = network.CreateTransaction();

        transaction.Outputs.Add(new TxOut(Money.Satoshis(sendInfo.SendAmount), BitcoinAddress.Create(sendInfo.SendToAddress, network.BitcoinNetwork).ScriptPubKey));

        sendInfo.UnSignedTransaction = transaction;

        return transaction;
    }

    private static int FindInputIndex(Transaction transaction, OutPoint outpoint)
    {
        for (int i = 0; i < transaction.Inputs.Count; i++)
        {
            if (transaction.Inputs[i].PrevOut == outpoint)
                return i;
        }
        throw new InvalidOperationException($"Input with outpoint {outpoint} not found in transaction");
    }
}

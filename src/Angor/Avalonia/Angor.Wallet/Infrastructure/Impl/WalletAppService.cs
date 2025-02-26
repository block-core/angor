using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Angor.Wallet.Application;
using Angor.Wallet.Domain;
using Angor.Wallet.Infrastructure.Interfaces;
using Blockcore.NBitcoin.BIP39;
using CSharpFunctionalExtensions;

namespace Angor.Wallet.Infrastructure.Impl;

public class WalletAppService : IWalletAppService 
{
    public static readonly WalletId SingleWalletId = new(new Guid("8E3C5250-4E26-4A13-8075-0A189AEAF793"));
    private const string SingleWalletName = "<default>";
        
    private readonly ISensitiveWalletDataProvider sensitiveWalletDataProvider;
    private readonly IIndexerService _indexerService;
    private readonly IWalletFactory walletFactory;
    private readonly IWalletOperations _walletOperations;
    
    public WalletAppService(
        ISensitiveWalletDataProvider sensitiveWalletDataProvider,
        IIndexerService indexerService,
        IWalletFactory walletFactory,
        IWalletOperations walletOperations)
    {
        this.sensitiveWalletDataProvider = sensitiveWalletDataProvider;
        _indexerService = indexerService;
        this.walletFactory = walletFactory;
        _walletOperations = walletOperations;
    }

    public async Task<Result<IEnumerable<WalletMetadata>>> GetMetadatas()
    {
        return new List<WalletMetadata>
        {
            new(SingleWalletName, SingleWalletId)
        };
    }

    public async Task<Result<IEnumerable<BroadcastedTransaction>>> GetTransactions(WalletId walletId)
    {
        try
        {
            var sensitiveDataResult = await sensitiveWalletDataProvider.RequestSensitiveData(walletId);
            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<IEnumerable<BroadcastedTransaction>>(sensitiveDataResult.Error);
            }

            var (seed, passphrase) = sensitiveDataResult.Value;
            
            var walletWords = new WalletWords { Words = seed, Passphrase = passphrase };
            var transactions = new List<BroadcastedTransaction>();
                
            var accountInfo = _walletOperations.BuildAccountInfoForWalletWords(walletWords);
                
            // Update existing UTXOs information
            await _walletOperations.UpdateDataForExistingAddressesAsync(accountInfo);
            await _walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);

            foreach (var address in accountInfo.AllAddresses())
            {
                var (_, utxos) = await _walletOperations.FetchUtxoForAddressAsync(address.Address);
                    
                foreach (var utxo in utxos)
                {
                    var txInfo = await _indexerService.GetTransactionInfoByIdAsync(utxo.outpoint.transactionId);
                    if (txInfo == null) continue;

                    // Calculate the balance for this transaction
                    var txBalance = CalculateTransactionBalance(txInfo, address.Address);
                            
                    // Map inputs and outputs
                    var (inputs, outputs) = MapInputsAndOutputs(txInfo);
                            
                    transactions.Add(new BroadcastedTransaction(
                        Balance: new Balance(txBalance),
                        Id: txInfo.TransactionId,
                        WalletInputs: inputs.Where(i => i.Address == address.Address).Select(i => new TransactionInputInfo(i)),
                        WalletOutputs: outputs.Where(o => o.Address == address.Address).Select(o => new TransactionOutputInfo(o)),
                        AllInputs: inputs,
                        AllOutputs: outputs,
                        Fee: txInfo.Fee,
                        IsConfirmed: txInfo.Confirmations > 0,
                        BlockHeight: txInfo.BlockIndex,
                        BlockTime: DateTimeOffset.FromUnixTimeSeconds(txInfo.Timestamp),
                        RawJson: System.Text.Json.JsonSerializer.Serialize(txInfo)
                    ));
                }
            }

            return Result.Success(transactions.OrderByDescending(t => t.BlockTime ?? DateTimeOffset.MaxValue).AsEnumerable());
        }
        catch (Exception ex)
        {
            return Result.Failure<IEnumerable<BroadcastedTransaction>>($"Error getting transactions: {ex.Message}");
        }
    }

    public Task<Result<Balance>> GetBalance(WalletId walletId)
    {
        return GetTransactions(walletId).Map(txns => txns.Sum(x => x.Balance.Value)).Map(l => new Balance(l));
        
    }

    public async Task<Result<Fee>> EstimateFee(WalletId walletId, Amount amount, Address address, DomainFeeRate feeRate)
    {
        if (walletId != SingleWalletId)
            return Result.Failure<Fee>("Invalid wallet ID");

        try
        {
            var sensitiveDataResult = await sensitiveWalletDataProvider.RequestSensitiveData(walletId);
            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<Fee>(sensitiveDataResult.Error);
            }

            var (seed, passphrase) = sensitiveDataResult.Value;
            
            var walletWords = new WalletWords { Words = seed, Passphrase = passphrase };
            var accountInfo = _walletOperations.BuildAccountInfoForWalletWords(walletWords);
        
            // Update AccountInfo to ensure it has change addresses
            await _walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);
        
            var changeAddress = accountInfo.GetNextChangeReceiveAddress();
            if (string.IsNullOrEmpty(changeAddress))
                return Result.Failure<Fee>("No change address available");

            var sendInfo = new SendInfo
            {
                SendAmount = amount.Value,
                SendToAddress = address.Value,
                ChangeAddress = changeAddress,
                SendUtxos = _walletOperations.FindOutputsForTransaction(amount.Value, accountInfo)
                    .ToDictionary(data => data.UtxoData.outpoint.ToString(), data => data),
            };

            var estimatedFee = _walletOperations.CalculateTransactionFee(sendInfo, accountInfo, feeRate.SatsPerVByte);
            return Result.Success(new Fee((long)(estimatedFee * 100_000_000))); // Convertir BTC a sats
        }
        catch (Exception ex)
        {
            return Result.Failure<Fee>($"Error estimating fee: {ex.Message}");
        }
    }
    public async Task<Result<Address>> GetNextReceiveAddress(WalletId walletId)
    {
        if (walletId != SingleWalletId)
            return Result.Failure<Address>("Invalid wallet ID");

        try
        {
            var sensitiveDataResult = await sensitiveWalletDataProvider.RequestSensitiveData(walletId);
            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<Address>(sensitiveDataResult.Error);
            }

            var (seed, passphrase) = sensitiveDataResult.Value;
            var walletWords = new WalletWords { Words = seed, Passphrase = passphrase };
            var accountInfo = _walletOperations.BuildAccountInfoForWalletWords(walletWords);
            await _walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);

            var address = accountInfo.GetNextReceiveAddress();
            if (string.IsNullOrEmpty(address))
                return Result.Failure<Address>("No address available");

            return Result.Success(new Address(address));
        }
        catch (Exception ex)
        {
            return Result.Failure<Address>($"Error getting receive address: {ex.Message}");
        }
    }

    public async Task<Result<TxId>> SendAmount(WalletId walletId, Amount amount, Address address, DomainFeeRate feeRate)
    {
        if (walletId != SingleWalletId)
            return Result.Failure<TxId>("Invalid wallet ID");
        
        var finalFeeRate = feeRate.SatsPerVByte * 10000; 

        try
        {
            var sensitiveDataResult = await sensitiveWalletDataProvider.RequestSensitiveData(walletId);
            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<TxId>(sensitiveDataResult.Error);
            }

            var (seed, passphrase) = sensitiveDataResult.Value;
            
            var walletWords = new WalletWords { Words = seed, Passphrase = passphrase };
            var accountInfo = _walletOperations.BuildAccountInfoForWalletWords(walletWords);
            await _walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);
            
            var sendInfo = new SendInfo
            {
                SendAmount = amount.Value,
                SendToAddress = address.Value,
                FeeRate = finalFeeRate,
                ChangeAddress = accountInfo.GetNextChangeReceiveAddress(),
                SendUtxos = _walletOperations.FindOutputsForTransaction(amount.Value, accountInfo)
                    .ToDictionary(data => data.UtxoData.outpoint.ToString(), data => data),
            };
            
            var fee = _walletOperations.CalculateTransactionFee(sendInfo, accountInfo, finalFeeRate);
            
            sendInfo.SendFee = fee;
            
            var result = await _walletOperations.SendAmountToAddress(walletWords, sendInfo);
            if (!result.Success)
                return Result.Failure<TxId>(result.Message);

            return Result.Success(new TxId(result.Data.GetHash().ToString()));
        }
        catch (Exception ex)
        {
            return Result.Failure<TxId>($"Error sending amount: {ex.Message}");
        }
    }

    public Task<Result<WalletId>> CreateWallet(string name, string seedWords, Maybe<string> passphrase, string encryptionKey, BitcoinNetwork network)
    {
        return walletFactory.CreateWallet(SingleWalletName, seedWords, passphrase, encryptionKey, network)
            .Map(_ => SingleWalletId);
    }

    public string GetSeedWords()
    {
        var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
        return mnemonic.ToString();
    }

    private (List<TransactionAddressInfo> inputs, List<TransactionAddressInfo> outputs) MapInputsAndOutputs(QueryTransaction tx)
    {
        var inputs = tx.Inputs.Select(input => new TransactionAddressInfo(
            input.InputAddress,
            input.InputAmount
        )).ToList();

        var outputs = tx.Outputs.Select(output => new TransactionAddressInfo(
            output.Address,
            output.Balance
        )).ToList();

        return (inputs, outputs);
    }

    private long CalculateTransactionBalance(QueryTransaction tx, string walletAddress)
    {
        var outputAmount = tx.Outputs
            .Where(o => o.Address == walletAddress)
            .Sum(o => o.Balance);

        var inputAmount = tx.Inputs
            .Where(i => i.InputAddress == walletAddress)
            .Sum(i => i.InputAmount);

        return outputAmount - inputAmount;
    }
}
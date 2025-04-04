using Angor.Contests.CrossCutting;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.NBitcoin.BIP39;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Infrastructure.Impl;

public class WalletAppService : IWalletAppService 
{
    public static readonly WalletId SingleWalletId = new(new Guid("8E3C5250-4E26-4A13-8075-0A189AEAF793"));
    private const string SingleWalletName = "<default>";
        
    private readonly ISensitiveWalletDataProvider sensitiveWalletDataProvider;
    private readonly IIndexerService indexerService;
    private readonly IWalletFactory walletFactory;
    private readonly IWalletOperations walletOperations;
    private readonly IWalletStore walletStore;

    public WalletAppService(
        ISensitiveWalletDataProvider sensitiveWalletDataProvider,
        IIndexerService indexerService,
        IWalletFactory walletFactory,
        IWalletOperations walletOperations, IWalletStore walletStore)
    {
        this.sensitiveWalletDataProvider = sensitiveWalletDataProvider;
        this.indexerService = indexerService;
        this.walletFactory = walletFactory;
        this.walletOperations = walletOperations;
        this.walletStore = walletStore;
    }

    public Task<Result<IEnumerable<WalletMetadata>>> GetMetadatas()
    {
        List<WalletMetadata> singleWalletList = [new(SingleWalletName, SingleWalletId)];
        return walletStore.GetAll().Map(wallets => wallets.Any() ? singleWalletList : []).Map(metadatas => metadatas.AsEnumerable());
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

            var sensitiveData = sensitiveDataResult.Value;
            
            var walletWords = sensitiveData.ToWalletWords();
            var transactions = new List<BroadcastedTransaction>();
                
            var accountInfo = walletOperations.BuildAccountInfoForWalletWords(walletWords);
                
            // Update existing UTXOs information
            await walletOperations.UpdateDataForExistingAddressesAsync(accountInfo);
            await walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);

            foreach (var address in accountInfo.AllAddresses())
            {
                var (_, utxos) = await walletOperations.FetchUtxoForAddressAsync(address.Address);
                    
                foreach (var utxo in utxos)
                {
                    var txInfo = await indexerService.GetTransactionInfoByIdAsync(utxo.outpoint.transactionId);
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

        var satsPerVirtualKB = feeRate.SatsPerVByte * 1000;
        
        try
        {
            var sensitiveDataResult = await sensitiveWalletDataProvider.RequestSensitiveData(walletId);
            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<Fee>(sensitiveDataResult.Error);
            }

            var walletWords = sensitiveDataResult.Value.ToWalletWords();
            var accountInfo = walletOperations.BuildAccountInfoForWalletWords(walletWords);
        
            // Update AccountInfo to ensure it has change addresses
            await walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);
        
            var changeAddress = accountInfo.GetNextChangeReceiveAddress();
            if (string.IsNullOrEmpty(changeAddress))
                return Result.Failure<Fee>("No change address available");

            var sendInfo = new SendInfo
            {
                FeeRate = satsPerVirtualKB,
                SendAmount = amount.Value,
                SendToAddress = address.Value,
                ChangeAddress = changeAddress,
                SendUtxos = walletOperations.FindOutputsForTransaction(amount.Value, accountInfo)
                    .ToDictionary(data => data.UtxoData.outpoint.ToString(), data => data),
            };

            var estimatedFee = walletOperations.CalculateTransactionFee(sendInfo, accountInfo, feeRate.SatsPerVByte);
            return Result.Success(new Fee((long)(estimatedFee * 100_000_000))); // Conversion to sats
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
            var walletWords = new WalletWords { Words = seed, Passphrase = passphrase.GetValueOrDefault("") };
            var accountInfo = walletOperations.BuildAccountInfoForWalletWords(walletWords);
            await walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);

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
        
        var satsPerVirtualKB = feeRate.SatsPerVByte * 1000; 

        try
        {
            var sensitiveDataResult = await sensitiveWalletDataProvider.RequestSensitiveData(walletId);
            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<TxId>(sensitiveDataResult.Error);
            }

            var (seed, passphrase) = sensitiveDataResult.Value;
            
            var walletWords = new WalletWords { Words = seed, Passphrase = passphrase.GetValueOrDefault("") };
            var accountInfo = walletOperations.BuildAccountInfoForWalletWords(walletWords);
            await walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);
            
            var sendInfo = new SendInfo
            {
                SendAmount = amount.Value,
                SendToAddress = address.Value,
                FeeRate = satsPerVirtualKB,
                ChangeAddress = accountInfo.GetNextChangeReceiveAddress(),
                SendUtxos = walletOperations.FindOutputsForTransaction(amount.Value, accountInfo)
                    .ToDictionary(data => data.UtxoData.outpoint.ToString(), data => data),
            };
            
            var fee = walletOperations.CalculateTransactionFee(sendInfo, accountInfo, satsPerVirtualKB);
            
            sendInfo.SendFee = fee;
            
            var result = await walletOperations.SendAmountToAddress(walletWords, sendInfo);
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

    public string GenerateRandomSeedwords()
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
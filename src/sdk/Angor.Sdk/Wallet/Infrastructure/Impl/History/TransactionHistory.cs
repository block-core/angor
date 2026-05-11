using System.Text.Json;
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;
using Angor.Sdk.Wallet.Infrastructure.History;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Angor.Primitives;
using Serilog;

namespace Angor.Sdk.Wallet.Infrastructure.Impl.History;

public class TransactionHistory(
    IIndexerService indexerService,
    IWalletAccountBalanceService accountBalanceService,
    ILogger logger) : ITransactionHistory
{
    public async Task<Result<IEnumerable<string>>> GetWalletAddresses(WalletId walletId)
    {
        var accountBalanceInfo =
            await accountBalanceService
                .RefreshAccountBalanceInfoAsync(walletId);

        if (accountBalanceInfo.IsFailure)
            return Result.Failure<IEnumerable<string>>(accountBalanceInfo.Error);

        return Result.Success(accountBalanceInfo.Value.AccountInfo.AllAddresses().Select(a => a.Address));
    }

    private async Task<Result<List<QueryTransaction>>> LookupAddressTransactions(string address)
    {
        try
        {
            var txns = await indexerService.FetchAddressHistoryAsync(address); //TODO handle paging with sending the last received transaction id
            return Result.Success(txns ?? new List<QueryTransaction>());
        }
        catch (Exception ex)
        {
            return Result.Failure<List<QueryTransaction>>(ex.Message);
        }
    }

    public async Task<Result<IEnumerable<BroadcastedTransaction>>> GetTransactions(WalletId walletId)
    {
        var result = await GetWalletAddresses(walletId);
        if (result.IsFailure)
        {
            return Result.Failure<IEnumerable<BroadcastedTransaction>>(result.Error);
        }
        var addressList = result.Value.ToList();
        var allTransactions = new List<QueryTransaction>();
        foreach (var address in addressList)
        {
            var txResult = await LookupAddressTransactions(address);
            if (txResult.IsFailure)
                return Result.Failure<IEnumerable<BroadcastedTransaction>>(txResult.Error);
            allTransactions.AddRange(txResult.Value);
        }

        var walletAddresses = addressList.Select(x => new Address(x)).ToList();
        var broadcastedTransactions = new List<BroadcastedTransaction>();
        foreach (var tx in allTransactions)
        {
            var mapped = await MapToBroadcastedTransaction(tx, walletAddresses);
            if (mapped.IsFailure)
                return Result.Failure<IEnumerable<BroadcastedTransaction>>(mapped.Error);
            broadcastedTransactions.Add(mapped.Value);
        }

        return Result.Success<IEnumerable<BroadcastedTransaction>>(broadcastedTransactions);

    }

    private static Task<Result<BroadcastedTransaction>> MapToBroadcastedTransaction(QueryTransaction tx,
        List<Address> walletAddresses)
    {
        var inputs = MapToTransactionInputs(tx.Inputs);
        var outputs = MapToTransactionOutputs(tx.Outputs);

        try
        {
            var broadcastedTx = new BroadcastedTransaction(
                Id: tx.TransactionId,
                WalletInputs: inputs
                    .Where(i => walletAddresses.Contains(i.Address)),
                WalletOutputs: outputs.Where(o => walletAddresses.Contains(o.Address)),
                AllInputs: inputs,
                AllOutputs: outputs,
                Fee: tx.Fee,
                IsConfirmed: tx.Confirmations > 0,
                BlockHeight: tx.BlockIndex,
                BlockTime: tx.Timestamp > 0 ? DateTimeOffset.FromUnixTimeSeconds(tx.Timestamp) : null,
                RawJson: JsonSerializer.Serialize(tx)
            );
            return Task.FromResult(Result.Success(broadcastedTx));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure<BroadcastedTransaction>(ex.Message));
        }
    }

    private static List<TransactionInput> MapToTransactionInputs(IEnumerable<QueryTransactionInput> inputs)
    {
        return inputs.Select(i => 
                new TransactionInput(new Amount(i.InputAmount), new Address(i.InputAddress)))
            .ToList();
    }

    private static List<TransactionOutput> MapToTransactionOutputs(IEnumerable<QueryTransactionOutput> outputs)
    {
        return outputs.Select(output => new TransactionOutput(new Amount(output.Balance), new Address(output.Address)))
            .ToList();
    }
}
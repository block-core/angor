using System.Text.Json;
using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.History;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Serilog;

namespace Angor.Contexts.Wallet.Infrastructure.Impl.History;

public class TransactionHistory(
    IIndexerService indexerService,
    IWalletOperations walletOperations,
    ILogger logger) : ITransactionHistory
{
    public Task<Result<IEnumerable<string>>> GetWalletAddresses(WalletWords walletWords)
    {
        return Result.Try(async () =>
        {
            var accountInfo = walletOperations.BuildAccountInfoForWalletWords(walletWords);

            await walletOperations.UpdateDataForExistingAddressesAsync(accountInfo);
            await walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);

            return accountInfo.AllAddresses().Select(a => a.Address);
        });
    }
    
    private Task<Result<List<QueryTransaction>>> LookupAddressTransactions(string address)
    {
        return Result.Try(() => indexerService.FetchAddressHistoryAsync(address)) //TODO handle paging with sending the last received transaciton id
            .Map(txns => txns ?? new List<QueryTransaction>());
    }

    public async Task<Result<IEnumerable<BroadcastedTransaction>>> GetTransactions(WalletWords walletWords)
    {
        var result = await GetWalletAddresses(walletWords);
        if (result.IsFailure)
        {
            return Result.Failure<IEnumerable<BroadcastedTransaction>>(result.Error);
        }
        return await result
            .Bind(addresses => addresses
                .Select(s => LookupAddressTransactions(s))
                .Combine())
            .Map(transactions => transactions
                .SelectMany(t => t))
            .Bind(transactions =>
            {
                var addresses = result.Value.Select(x => new Address(x)).ToList();
                return transactions
                    .Select(tx => MapToBroadcastedTransaction(tx, addresses))
                    .Combine();
            });

    }

    private static Task<Result<BroadcastedTransaction>> MapToBroadcastedTransaction(QueryTransaction tx,
        List<Address> walletAddresses)
    {
        var inputs = MapToUiModelInputs(tx.Inputs);
        var outputs = MapToUiModelOutputs(tx.Outputs);

        return Task.FromResult(Result.Try(() => new BroadcastedTransaction(
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
        )));
    }

    private static List<TransactionInput> MapToUiModelInputs(IEnumerable<QueryTransactionInput> inputs)
    {
        return inputs.Select(i => 
                new TransactionInput(new Amount(i.InputAmount), new Address(i.InputAddress)))
            .ToList();
    }

    private static List<TransactionOutput> MapToUiModelOutputs(IEnumerable<QueryTransactionOutput> outputs)
    {
        return outputs.Select(output => new TransactionOutput(new Amount(output.Balance), new Address(output.Address)))
            .ToList();
    }
}
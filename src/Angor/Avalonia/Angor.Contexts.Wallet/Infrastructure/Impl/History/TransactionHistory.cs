using System.Text.Json;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.History;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Serilog;

namespace Angor.Contexts.Wallet.Infrastructure.Impl.History;

public class TransactionHistory(
    IHttpClientFactory httpClientFactory,
    INetworkService networkService,
    IWalletOperations walletOperations, 
    ILogger logger) : ITransactionHistory
{
    [MemoizeTimed]
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

    public Task<Result<List<QueryAddressItem>>> GetTransactions(string address, int offset = 0, int limit = 50)
    {
        return QueryIndexer<List<QueryAddressItem>>($"api/query/address/{address}/transactions?offset={offset}&limit={limit}");
    }
    
    private Task<Result<QueryTransaction>> GetTransactionInfo(string txId)
    {
        return QueryIndexer<QueryTransaction>($"api/query/transaction/{txId}");
    }
    
    [MemoizeTimed]
    private Task<Result<TModel>> QueryIndexer<TModel>(string address) where TModel : class
    {
        var transactionIds = Result.Try(() => networkService.GetPrimaryIndexer())
            .Map(indexerUrl => new Uri(new Uri(indexerUrl.Url), address))
            .MapTry(url => new { client = httpClientFactory.CreateClient("IndexerClient"), url})
            .MapTry(request => request.client.GetAsync(request.url))
            .Ensure(x => x.IsSuccessStatusCode, x => $"Could not get transaction history: Code: {x.StatusCode}, Reason {x.ReasonPhrase}")
            .MapTry(message => message.Content.ReadAsStringAsync())
            .Tap(content => logger.Debug("Trying to deserialize {Content} to {Type}", content, typeof(TModel)))
            .MapTry(s => JsonSerializer.Deserialize<TModel>(s, JsonSerializerOptions.Web), exception => $"Error deserializing content to {typeof(TModel)}: {exception.Message}.")
            .EnsureNotNull("The response was null");
        
        return transactionIds;
    }

    public async Task<Result<IEnumerable<BroadcastedTransaction>>> GetTransactions(WalletWords walletWords)
    {
        return await GetWalletAddresses(walletWords)
            .Bind(addresses => addresses.Select(s => GetTransactions(s)).Combine())
            .Map(txns => txns.SelectMany(x => x))
            .Map(queryAddressItems => queryAddressItems.Select(queryAddressItem => queryAddressItem.TransactionHash).Distinct())
            .Bind(uniqueTxIds => uniqueTxIds.Select(txn => GetTransactionInfo(txn)).Combine())
            .Bind(transactions => transactions.Select(tx => CreateBroadcastedTransaction(tx, walletWords)).Combine());
    }

    private Task<Result<BroadcastedTransaction>> CreateBroadcastedTransaction(QueryTransaction tx, WalletWords walletWords)
    {
        var (inputs, outputs) = MapInputsAndOutputs(tx);

        return GetWalletAddresses(walletWords)
            .Map(enumerable => enumerable.Select(a => new Address(a)))
            .Map(walletAddresses =>
        {
            return new BroadcastedTransaction(
                Id: tx.TransactionId,
                WalletInputs: inputs.Where(i => walletAddresses.Contains(i.Address)),
                WalletOutputs: outputs.Where(o => walletAddresses.Contains(o.Address)),
                AllInputs: inputs,
                AllOutputs: outputs,
                Fee: tx.Fee,
                IsConfirmed: tx.Confirmations > 0,
                BlockHeight: tx.BlockIndex,
                BlockTime: tx.Timestamp > 0 ? DateTimeOffset.FromUnixTimeSeconds(tx.Timestamp) : null,
                RawJson: JsonSerializer.Serialize(tx)
            );
        });
    }

    private static (List<TransactionInput> inputs, List<TransactionOutput> outputs) MapInputsAndOutputs(QueryTransaction tx)
    {
        var inputs = tx.Inputs.Select(input => new TransactionInput(
            new Amount(input.InputAmount),
            new Address(input.InputAddress)
        )).ToList();

        var outputs = tx.Outputs.Select(output => new TransactionOutput(
            new Amount(output.Balance),
            new Address(output.Address)
        )).ToList();

        return (inputs, outputs);
    }
}
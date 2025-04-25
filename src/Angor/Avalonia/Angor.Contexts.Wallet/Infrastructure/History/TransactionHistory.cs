using System.Text.Json;
using Angor.Contexts.Wallet.Domain;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Serilog;

namespace Angor.Contexts.Wallet.Infrastructure.History;

public class TransactionHistory(
    IHttpClientFactory httpClientFactory,
    INetworkService networkService,
    IWalletOperations walletOperations, 
    ILogger logger)
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

    public Task<Result<List<QueryAddressItem>>> GetTransactions(string address, int offset = 0, int limit = 50)
    {
        return QueryIndexer<List<QueryAddressItem>>($"api/query/address/{address}/transactions?offset={offset}&limit={limit}");
    }
    
    private Task<Result<QueryTransaction>> GetTransactionInfo(string txId)
    {
        return QueryIndexer<QueryTransaction>($"api/query/transaction/{txId}");
    }
    
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

    public async Task<Result<BroadcastedTransaction>> GetTransactions(WalletWords walletWords)
    {
        var result = await GetWalletAddresses(walletWords)
            .Bind(addresses => addresses.Select(s => GetTransactions(s)).Combine())
            .Map(txIds => txIds.SelectMany(x => x))
            .Bind(uniqueTxIds => uniqueTxIds.Select(txn => GetTransactionInfo(txn.TransactionHash)).Combine())
            .Map(transactions => transactions.Select(tx => CreateBroadcastedTransaction(tx)));
        
        return new Result<BroadcastedTransaction>();
    }

    private BroadcastedTransaction CreateBroadcastedTransaction(QueryTransaction tx)
    {
        // Extraer información de direcciones e inputs/outputs
        var (inputs, outputs) = MapInputsAndOutputs(tx);

        // Determinar si hay alguna dirección de la cartera involucrada
        var walletAddresses = GetWalletAddressesFromTransaction(tx);
    
        // Calcular el balance de la transacción
        var txBalance = CalculateTransactionBalance(tx, walletAddresses);
    
        return new BroadcastedTransaction(
            Balance: new Balance(txBalance),
            Id: tx.TransactionId,
            WalletInputs: inputs.Where(i => walletAddresses.Contains(i.Address))
                .Select(i => new TransactionInputInfo(i)),
            WalletOutputs: outputs.Where(o => walletAddresses.Contains(o.Address))
                .Select(o => new TransactionOutputInfo(o)),
            AllInputs: inputs,
            AllOutputs: outputs,
            Fee: tx.Fee,
            IsConfirmed: tx.Confirmations > 0,
            BlockHeight: tx.BlockIndex,
            BlockTime: tx.Timestamp > 0 ? DateTimeOffset.FromUnixTimeSeconds(tx.Timestamp) : null,
            RawJson: JsonSerializer.Serialize(tx)
        );
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

    private HashSet<string> GetWalletAddressesFromTransaction(QueryTransaction tx)
    {
        // Normalmente esto involucraría verificar qué direcciones pertenecen a la cartera
        // Para este ejemplo, usamos una aproximación simplificada basada en la información disponible
        return new HashSet<string>();
    }

    private long CalculateTransactionBalance(QueryTransaction tx, HashSet<string> walletAddresses)
    {
        var outputAmount = tx.Outputs
            .Where(o => walletAddresses.Contains(o.Address))
            .Sum(o => o.Balance);

        var inputAmount = tx.Inputs
            .Where(i => walletAddresses.Contains(i.InputAddress))
            .Sum(i => i.InputAmount);

        return outputAmount - inputAmount;
    }

}
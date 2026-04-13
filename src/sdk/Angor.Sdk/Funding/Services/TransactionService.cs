using Angor.Sdk.Funding.Projects.Domain;
using Angor.Data.Documents.Interfaces;
using Angor.Shared.Models;
using Angor.Shared.Services;

namespace Angor.Sdk.Funding.Services;

public class TransactionService(IGenericDocumentCollection<QueryTransaction> queryTransactionCollection,
    IGenericDocumentCollection<TransactionHexDocument> trxHexCollection,
    IIndexerService indexerService) : ITransactionService
{
    public async Task<string?> GetTransactionHexByIdAsync(string transactionId)
    {
        var hexDocumentResult = await trxHexCollection.FindByIdAsync(transactionId);
           
        var transactionHex = hexDocumentResult.Value?.Hex;

        if (hexDocumentResult.IsFailure || hexDocumentResult.Value is null)
        {
            transactionHex = await indexerService.GetTransactionHexByIdAsync(transactionId);
        }

        if (hexDocumentResult.Value is null && !string.IsNullOrEmpty(transactionHex))
        {
            // store the hex for future use
            var insertResult = await trxHexCollection.UpsertAsync(
                x => x.Id
                , new TransactionHexDocument
                {
                    Id = transactionId,
                    Hex = transactionHex
                });

            //TODO log the insert result?
        }

        return transactionHex;
    }

    public Task SaveTransactionHexAsync(string transactionId, string transactionHex)
    {
        if (string.IsNullOrEmpty(transactionId) || string.IsNullOrEmpty(transactionHex))
            return Task.CompletedTask;

        return trxHexCollection.UpsertAsync(x => x.Id,
            new TransactionHexDocument { Id = transactionId, Hex = transactionHex });
    }

    public async Task<QueryTransaction?> GetTransactionInfoByIdAsync(string transactionId)
    {
         var trxInfoResult = await queryTransactionCollection.FindByIdAsync(transactionId);
            
        var trxInfo = trxInfoResult.Value;
        
        if (trxInfo is null)
        {
            trxInfo = await indexerService.GetTransactionInfoByIdAsync(transactionId);
            if (trxInfo is not null)
            {
                var insertResult = await queryTransactionCollection
                    .UpsertAsync(x => x.TransactionId, trxInfo);
            }
        }
        else if (trxInfo.Confirmations == 0 || trxInfo.Outputs.Any(IsUnspent))
        {
            // Re-fetch if unconfirmed (timestamp needed for penalty expiry)
            // or if there are unspent outputs that may now be spent
            var refreshed = await indexerService.GetTransactionInfoByIdAsync(transactionId);
            if (refreshed is not null)
            {
                trxInfo = refreshed;
                await queryTransactionCollection.UpdateAsync(x => x.TransactionId, trxInfo);
            }
        }
        
        return trxInfo;
    }

    private static bool IsUnspent(QueryTransactionOutput output) => 
        string.IsNullOrEmpty(output.SpentInTransaction) && output.OutputType != "op_return";

    public Task SaveQueryTransactionAsync(QueryTransaction document)
    {
        return queryTransactionCollection.UpsertAsync(x => x.TransactionId, document);
    }
}

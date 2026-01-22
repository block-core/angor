using Angor.Sdk.Funding.Projects.Domain;
using Angor.Data.Documents.Interfaces;
using Angor.Shared.Models;
using Angor.Shared.Services.Indexer;

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
            var insertResult = await trxHexCollection.InsertAsync(
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

        return trxHexCollection.InsertAsync(x => x.Id,
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
                    .InsertAsync(x => x.TransactionId, trxInfo);    
            }
        }
        else if(trxInfo.Outputs.Any(IsUnspent)) //If we have unspent outputs, check if they are still unspent
        {
            var spent = await indexerService.GetIsSpentOutputsOnTransactionAsync(transactionId);
            var outputs = trxInfo.Outputs.ToList();
            
            if (!spent.Any(info => info.spent && IsUnspent(outputs[info.index]))) 
                return trxInfo;
            
            trxInfo = await indexerService.GetTransactionInfoByIdAsync(transactionId);
            if (trxInfo is not null)
            {
                var insertResult = await queryTransactionCollection
                    .UpdateAsync(x => x.TransactionId, trxInfo);
            }
        }
        
        return trxInfo;
    }

    private static bool IsUnspent(QueryTransactionOutput output) => 
        string.IsNullOrEmpty(output.SpentInTransaction) && output.OutputType != "op_return";

    public Task SaveQueryTransactionAsync(QueryTransaction document)
    {
        return queryTransactionCollection.InsertAsync(x => x.TransactionId, document);
    }
}
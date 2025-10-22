using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Data.Documents.Interfaces;
using Angor.Shared.Models;
using Angor.Shared.Services;

namespace Angor.Contexts.Funding.Projects.Infrastructure.Impl;

public class TransactionRepository(IGenericDocumentCollection<QueryTransaction> queryTransactionCollection,
    IGenericDocumentCollection<TransactionHexDocument> trxHexCollection,
    IIndexerService indexerService) : ITransactionRepository
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

        if (trxInfoResult.IsFailure || trxInfoResult.Value is null)
        {
            trxInfo = await indexerService.GetTransactionInfoByIdAsync(transactionId);
        }

        if (trxInfoResult.Value is null && trxInfo != null)
        {
            var insertResult = await queryTransactionCollection.InsertAsync(x => x.TransactionId, trxInfo);

            //TODO log the insert result?
        }

        return trxInfo;
    }

    public Task SaveQueryTransactionAsync(QueryTransaction document)
    {
        return queryTransactionCollection.InsertAsync(x => x.TransactionId, document);
    }
}
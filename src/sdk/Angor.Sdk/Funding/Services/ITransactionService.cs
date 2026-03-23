using Angor.Shared.Models;

namespace Angor.Sdk.Funding.Services;

public interface ITransactionService
{
    Task<string?> GetTransactionHexByIdAsync(string transactionId);
    
    Task SaveTransactionHexAsync(string transactionId, string transactionHex);
    
    Task<QueryTransaction?> GetTransactionInfoByIdAsync(string transactionId);
    
    Task SaveQueryTransactionAsync(QueryTransaction document);
}
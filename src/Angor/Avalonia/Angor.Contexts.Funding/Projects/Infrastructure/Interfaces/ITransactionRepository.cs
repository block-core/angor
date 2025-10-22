using Angor.Shared.Models;

namespace Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;

public interface ITransactionRepository
{
    Task<string?> GetTransactionHexByIdAsync(string transactionId);
    
    Task SaveTransactionHexAsync(string transactionId, string transactionHex);
    
    Task<QueryTransaction?> GetTransactionInfoByIdAsync(string transactionId);
    
    Task SaveQueryTransactionAsync(QueryTransaction document);
}
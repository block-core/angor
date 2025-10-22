using Angor.Data.Documents.Interfaces;
using Angor.Shared.Models;

namespace Angor.Contexts.Funding.Projects.Domain;

public class QueryTransactionDocument : IDocumentEntity
{
    public required QueryTransaction QueryTransaction { get; set; }

    public string GetDocumentId() => QueryTransaction.TransactionId;
}
using Angor.Data.Documents.Interfaces;

namespace Angor.Contexts.Funding.Projects.Domain;

public class TransactionHexDocument : IDocumentEntity
{
    public required string Hex { get; set; }
    public required string Id { get; set; }
    public string GetDocumentId() => Id;
}
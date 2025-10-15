namespace Angor.Data.Documents.Interfaces;

public interface IAngorDocumentDatabaseFactory
{
    IAngorDocumentDatabase CreateDatabase(string profileName);
}
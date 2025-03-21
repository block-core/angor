namespace Angor.Shared.Services;

public interface INostrEncryptionService
{
    string EncryptNostrContent(string nsec, string npub, string content);
    Task<string> EncryptNostrContentAsync(string nsec,string npub, string content);
}
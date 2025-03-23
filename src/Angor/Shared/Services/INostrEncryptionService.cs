namespace Angor.Shared.Services;

public interface INostrEncryptionService
{
    Task<string> EncryptNostrContentAsync(string nsec,string npub, string content);
    
    Task<string> DecryptNostrContentAsync(string nsec, string npub, string encryptedContent);
}
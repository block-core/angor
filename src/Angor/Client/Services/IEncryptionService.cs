using Nostr.Client.Utils;

namespace Angor.Client.Services
{
    public interface IEncryptionService
    {
        Task<string> EncryptData(string secretData, string password);
        Task<string> DecryptData(string encryptedData, string password);
        
        Task<string> EncryptNostrContentAsync(string nsec,string npub, string content);
        Task<string> DecryptNostrContentAsync(string nsec, string npub, string encrptedContent);
    }

}

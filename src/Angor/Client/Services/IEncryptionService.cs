using Nostr.Client.Utils;

namespace Angor.Client.Services
{
    public interface IEncryptionService
    {
        Task<string> EncryptData(string secretData, string password);
        Task<string> DecryptData(string encryptedData, string password);
        
        Task<string> EncryptNostrContentAsync(string senderNsec, string recipientNpub, string content);
        Task<string> DecryptNostrContentAsync(string recipientNsec, string senderNpub, string encryptedContent);
    }

}

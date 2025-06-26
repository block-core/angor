using Angor.Shared.Services;
using Nostr.Client.Keys;
using Nostr.Client.Messages;
using Nostr.Client.Messages.Direct;
using Nostr.Client.Utils;

namespace Angor.Contexts.Funding.Shared;

public class NostrEncryption(ISerializer serializer) : INostrEncryption
{
    public Task<string> Nip4Encryption<T>(T content, string localPrivateKey, string remotePublicKey)
    {
        var privateKey = NostrPrivateKey.FromHex(localPrivateKey);
        var nostrPubKey = NostrPublicKey.FromHex(remotePublicKey);

        var parsedEvent = content as NostrEvent 
                          ?? new NostrEvent()
        {
            Content = serializer.Serialize(content),
            CreatedAt = DateTime.UtcNow,
            Kind = NostrKind.EncryptedDm,
            Pubkey = remotePublicKey,
        };

        var encryptedContent = parsedEvent.Encrypt(privateKey, nostrPubKey).EncryptedContent;
        
        return Task.FromResult(encryptedContent!);
    }

    public Task<T> Nip4Decryption<T>(string content, string localPrivateKey, string remotePublicKey)
    {
        try
        {
            var privateKey = NostrPrivateKey.FromHex(localPrivateKey);
            var nostrPubKey = NostrPublicKey.FromHex(remotePublicKey);
            
            var decryptedContent = new NostrEncryptedEvent(content,new NostrEventTags(new []{NostrEventTag.Profile(nostrPubKey.Hex)}))
                {Pubkey = nostrPubKey.Hex}.DecryptContent(privateKey);
            return Task.FromResult(serializer.Deserialize<T>(decryptedContent));
        }
        catch (Exception e)
        {
            return Task.FromException<T>(e);
        }
    }
}
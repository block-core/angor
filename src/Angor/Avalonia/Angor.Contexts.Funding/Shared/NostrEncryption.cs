using Angor.Shared.Services;
using Nostr.Client.Keys;
using Nostr.Client.Messages;

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

        var encryptedContent = parsedEvent.EncryptDirect(privateKey, nostrPubKey).EncryptedContent;
        
        return Task.FromResult(encryptedContent!);
    }
}
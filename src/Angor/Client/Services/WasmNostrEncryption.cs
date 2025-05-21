using Angor.Shared.Services;
using Nostr.Client.Messages;

namespace Angor.Client.Services;

public class WasmNostrEncryption : INostrEncryption
{
    private readonly IEncryptionService encryptionService;
    private readonly ISerializer serializer;

    public WasmNostrEncryption(IEncryptionService encryptionService, ISerializer serializer)
    {
        this.encryptionService = encryptionService;
        this.serializer = serializer;
    }
    
    public async Task<NostrEvent> Encrypt(NostrEvent ev, string localPrivateKey, string remotePublicKey)
    {
        var encryptedContent = await encryptionService.EncryptNostrContentAsync(localPrivateKey,remotePublicKey, serializer.Serialize(ev));

        var newEvent = new NostrEvent
        {
            Content = encryptedContent,
            AdditionalData = ev.AdditionalData,
            CreatedAt = ev.CreatedAt,
            Kind = ev.Kind,
            Pubkey = ev.Pubkey,
            Tags = ev.Tags,
        };
        
        return newEvent;
    }
}
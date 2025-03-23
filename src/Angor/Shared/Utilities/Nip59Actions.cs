using System.Text;
using Angor.Shared.Services;
using Newtonsoft.Json;
using Nostr.Client.Json;
using Nostr.Client.Keys;
using Nostr.Client.Messages;

namespace Angor.Shared.Utilities;

public class Nip59Actions : INostrNip59Actions
{
    private INostrEncryptionService _nostrEncryptionService;
    private ISerializer _serializer;

    public Nip59Actions(INostrEncryptionService nostrEncryptionService, ISerializer serializer)
    {
        _nostrEncryptionService = nostrEncryptionService;
        _serializer = serializer;
    }

    public async Task<NostrEvent> SealEvent(NostrEvent rumor, NostrPrivateKey privateKey, string recipientNpub)
    {
        var stringWriter = new StringWriter(new StringBuilder());
        var jsonWriter = new JsonTextWriter(stringWriter);
        NostrSerializer.Serializer.Serialize(jsonWriter,rumor);
            
        var test = _serializer.Serialize(rumor);
            
        Console.Write(test);
        Console.Write(stringWriter.ToString());
            
        var encryptNostrContent = await _nostrEncryptionService.EncryptNostrContentAsync(
            privateKey.Hex, recipientNpub, stringWriter.ToString());

        var sealedEvent = new NostrEvent
        {
            Kind = (NostrKind)13,
            Content = encryptNostrContent,
            CreatedAt = DateTime.UtcNow //TODO change to a random time
        };
            
        return sealedEvent.Sign(privateKey);
    }

    public async Task<NostrEvent> WrapEventAsync(NostrEvent nostrEvent, string recipeintNpub,
        string? subject = null, string? replyTo = null)
    {
        var ephemeralKey = NostrPrivateKey.GenerateNew();

        var encryptedEvent = await _nostrEncryptionService.EncryptNostrContentAsync(ephemeralKey.Hex,
            recipeintNpub, _serializer.Serialize(nostrEvent));

        var taglist = new List<NostrEventTag?>
        {
            NostrEventTag.Profile(recipeintNpub),
            replyTo != null ? NostrEventTag.Event(replyTo) : null,
            subject != null ? new NostrEventTag("subject", subject) : null,
        };

        var wrap = new NostrEvent
        {
            Kind = (NostrKind)1059,
            Content = encryptedEvent,
            CreatedAt = DateTime.UtcNow, //TODO change to a random time
            Tags = new NostrEventTags(taglist
                .Where(x => x != null)
                .Select(x => x!)
                .ToArray()),
        };

        return wrap.Sign(ephemeralKey);
    }

    public async Task<NostrEvent?> UnwrapEventAsync(NostrEvent signed, string recipientNsec)
    {
        var nostrPrivateKey = NostrPrivateKey.FromHex(recipientNsec);
            
        var recipientPubKey = signed.Tags.FindFirstTagValue(NostrEventTag.ProfileIdentifier);
            
        if (recipientPubKey != nostrPrivateKey.DerivePublicKey().Hex)
            return null;
            
        var seal = await _nostrEncryptionService.DecryptNostrContentAsync(recipientNsec, signed.Pubkey, signed.Content!);
            
        var sealedEvent = NostrSerializer.Serializer.Deserialize<NostrEvent>(new JsonTextReader(new StringReader(seal)));

        var jsonEvent = await _nostrEncryptionService.DecryptNostrContentAsync(recipientNsec, sealedEvent.Pubkey, sealedEvent.Content);
            
        return NostrSerializer.Serializer.Deserialize<NostrEvent>(new JsonTextReader(new StringReader(jsonEvent)));
    }
}
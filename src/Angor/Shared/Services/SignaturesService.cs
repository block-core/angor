using CSharpFunctionalExtensions;
using Nostr.Client.Keys;
using Nostr.Client.Messages;

namespace Angor.Shared.Services;

public class SignaturesService(
    ISensitiveNostrData sensitiveNostrData,
    INostrEncryption nostrEncryption,
    INostrService nostrService,
    ISerializer serializer)
    : ISignaturesService
{
    public async Task<Result<ISignaturesService.EventSendResponse>> PostInvestmentRequest<T>(KeyIdentifier keyIdentifier, T content,
        string founderNostrPubKey)
    {
        var key = await sensitiveNostrData.GetNostrPrivateKeyHex(keyIdentifier);
        var parsedKey = NostrPrivateKey.FromHex(key.Value);
        var jsonContent = serializer.Serialize(content);
        var encryptedContent = await nostrEncryption.Nip4Encryption(jsonContent, key.Value, founderNostrPubKey);
        var ev = new NostrEvent
        {
            Kind = NostrKind.EncryptedDm,
            CreatedAt = DateTime.UtcNow,
            Content = encryptedContent,
            Tags = new NostrEventTags(
                NostrEventTag.Profile(founderNostrPubKey),
                new NostrEventTag("subject", "Investment offer"))
        };
            
        var signed = ev.Sign(parsedKey);

        return await nostrService.Send(signed)
            .Ensure(response => response.Accepted, "Failed to send event")
            .Map(response => new ISignaturesService.EventSendResponse(response.Accepted, response.EventId, response.Message,
                response.ReceivedTimestamp));
    }

    public Task<Result<ISignaturesService.EventSendResponse>> PostInvestmentRequestApproval<T>(KeyIdentifier keyIdentifier, T content,
        string investorNostrPubKey, string eventId)
    {
        return sensitiveNostrData.GetNostrPrivateKeyHex(keyIdentifier)
            .Map(key => (ParsedKey: NostrPrivateKey.FromHex(key), JsonContent: serializer.Serialize(content)))
            .Bind(data => Result.Try(() => nostrEncryption.Nip4Encryption(data.JsonContent, data.ParsedKey.Hex, investorNostrPubKey))
                .Ensure(s => !string.IsNullOrEmpty(s), "Failed to encrypt content")
                .Map((encryptedContent) => new { data.ParsedKey, encryptedContent }))
            .Map(data =>
            {
                var ev = new NostrEvent
                {
                    Kind = NostrKind.EncryptedDm,
                    CreatedAt = DateTime.UtcNow,
                    Content = data.encryptedContent,
                    Tags = new NostrEventTags(
                        NostrEventTag.Profile(investorNostrPubKey),
                        NostrEventTag.Event(eventId),
                        new NostrEventTag("subject", "Re:Investment offer"))
                };
                return ev.Sign(data.ParsedKey);
            })
            .Bind(data => nostrService.Send(data)
                .Ensure(response => response.Accepted, "Failed to send event"))
            .Map(response => new ISignaturesService.EventSendResponse(response.Accepted, response.EventId, response.Message,
                response.ReceivedTimestamp));
    }
}
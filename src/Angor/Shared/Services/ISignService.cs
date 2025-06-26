using Angor.Shared.Models;
using Nostr.Client.Responses;

namespace Angor.Shared.Services;

public interface ISignService
{
    (DateTime eventTime, string eventId) RequestInvestmentSigs(string encryptedContent, string investorNostrPrivateKey,
        string founderNostrPubKey, Action<NostrOkResponse> okResponse);
    void LookupSignatureForInvestmentRequest(string investorNostrPubKey, string projectNostrPubKey, DateTime? sigRequestSentTime, string sigRequestEventId, Func<string, Task> action,Action onAllMessagesReceived = null);

    Task LookupInvestmentRequestsAsync(string nostrPubKey, string? senderNpub, DateTime? since, Action<string, string, string, DateTime> action,
        Action onAllMessagesReceived);
    
    void LookupInvestmentRequestApprovals(string nostrPubKey, Action<string, DateTime, string> action,
        Action onAllMessagesReceived);

    DateTime SendSignaturesToInvestor(string encryptedSignatureInfo, string nostrPrivateKey,
        string investorNostrPubKey, string eventId);

    DateTime SendReleaseSigsToInvestor(string encryptedReleaseSigInfo, string nostrPrivateKeyHex, string investorNostrPubKey, string eventId);

    void LookupReleaseSigs(string investorNostrPubKey, string projectNostrPubKey, DateTime? releaseRequestSentTime, string releaseRequestEventId, Action<string> action, Action onAllMessagesReceived);

    void LookupSignedReleaseSigs(string projectNostrPubKey, Action<SignServiceLookupItem> action, Action onAllMessagesReceived);
}

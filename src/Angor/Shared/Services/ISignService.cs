using Angor.Shared.Models;

namespace Angor.Client.Services;

public interface ISignService
{
    (DateTime eventTime, string eventId) RequestInvestmentSigs(string encryptedContent, string investorNostrPrivateKey, string founderNostrPubKey);
    void LookupSignatureForInvestmentRequest(string investorNostrPubKey, string projectNostrPubKey, DateTime sigRequestSentTime, string sigRequestEventId, Func<string, Task> action);

    Task LookupInvestmentRequestsAsync(string nostrPubKey, string? senderNpub, DateTime? since, Action<string, string, string, DateTime> action,
        Action onAllMessagesReceived);
    
    void LookupInvestmentRequestApprovals(string nostrPubKey, Action<string, DateTime, string> action,
        Action onAllMessagesReceived);

    DateTime SendSignaturesToInvestor(string encryptedSignatureInfo, string nostrPrivateKey,
        string investorNostrPubKey, string eventId);

    DateTime SendReleaseSigsToInvestor(string encryptedReleaseSigInfo, string nostrPrivateKeyHex, string investorNostrPubKey, string eventId);

    void LookupReleaseSigs(string investorNostrPubKey, string projectNostrPubKey, DateTime? releaseRequestSentTime, string releaseRequestEventId, Func<string, Task> action);

    void LookupSignedReleaseSigs(string projectNostrPubKey, Action<SignServiceLookupItem> action, Action onAllMessagesReceived);
}
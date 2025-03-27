using Angor.Shared.Models;

namespace Angor.Shared.Services;

public interface ISignService
{
    Task<(DateTime eventTime, string eventId)> RequestInvestmentSigsAsync(string encryptedContent, string investorNostrPrivateKey,
        string founderNostrPubKey);
    void LookupSignatureForInvestmentRequest(string investorNostrPubKey, string projectNostrPubKey, DateTime? sigRequestSentTime, string sigRequestEventId, Func<string, Task> action);

    Task LookupInvestmentRequestsAsync(string founderNsec, string? senderNpub, DateTime? since, Action<string, string, string, DateTime> action,
        Action onAllMessagesReceived);
    
    void LookupInvestmentRequestApprovals(string nostrPubKey, Action<string, DateTime, string> action,
        Action onAllMessagesReceived);

    Task<DateTime> SendSignaturesToInvestorAsync(string encryptedSignatureInfo, string nostrPrivateKey,
        string investorNostrPubKey, string eventId);

    Task<DateTime> SendReleaseSigsToInvestorAsync(string encryptedReleaseSigInfo, string nostrPrivateKeyHex, string investorNostrPubKey, string eventId);

    void LookupReleaseSigs(string investorNostrPubKey, string projectNostrPubKey, DateTime? releaseRequestSentTime, string releaseRequestEventId, Action<string> action, Action onAllMessagesReceived);

    void LookupSignedReleaseSigs(string projectNostrPubKey, Action<SignServiceLookupItem> action, Action onAllMessagesReceived);
    
    
    
    Task LookupAllRequestsAsync(string founderNsec, Action<string,string, string, string, DateTime> action, Action onAllMessagesReceived);
    
    Task<string> SendApprovedListOfProjectsToStorageKeyAsync(string content, string storageKey, string founderPubKey);
}

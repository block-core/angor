using Angor.Shared.Models;
using Nostr.Client.Responses;

namespace Angor.Shared.Services;

/// <summary>
/// Types of investment-related messages that can be received via Nostr.
/// </summary>
public enum InvestmentMessageType
{
    /// <summary>Investment signature request ("Investment offer")</summary>
    Request,
    /// <summary>Investment completion notification ("Investment completed")</summary>
    Notification,
    /// <summary>Investment cancellation notification ("Investment canceled")</summary>
    Cancellation,
    /// <summary>Founder approval response ("Re:Investment offer")</summary>
    Approval
}

public interface ISignService
{
    (DateTime eventTime, string eventId) RequestInvestmentSigs(string encryptedContent, string investorNostrPrivateKey,
        string founderNostrPubKey, Action<NostrOkResponse> okResponse);
    
    /// <summary>
    /// Sends a notification to the founder that an investment has been completed (for below-threshold investments that don't require signatures).
    /// </summary>
    (DateTime eventTime, string eventId) NotifyInvestmentCompleted(string encryptedContent, string investorNostrPrivateKey,
        string founderNostrPubKey, Action<NostrOkResponse> okResponse);
    
    void LookupSignatureForInvestmentRequest(string investorNostrPubKey, string projectNostrPubKey, DateTime? sigRequestSentTime, string sigRequestEventId, Func<string, Task> action,Action? onAllMessagesReceived = null);

    Task LookupInvestmentRequestsAsync(string nostrPubKey, string? senderNpub, DateTime? since, Action<string, string, string, DateTime> action,
        Action onAllMessagesReceived);
    
    /// <summary>
    /// Looks up investment completion notifications (for below-threshold investments that don't require signatures).
    /// </summary>
    Task LookupInvestmentNotificationsAsync(string nostrPubKey, string? senderNpub, DateTime? since, Action<string, string, string, DateTime> action,
        Action onAllMessagesReceived);
    
    /// <summary>
    /// Sends an unencrypted notification to the founder that an investment request has been canceled.
    /// </summary>
    (DateTime eventTime, string eventId) NotifyInvestmentCanceled(string content, string investorNostrPrivateKey,
        string founderNostrPubKey, Action<NostrOkResponse> okResponse);

    /// <summary>
    /// Looks up investment cancellation notifications.
    /// </summary>
    Task LookupInvestmentCancellationsAsync(string nostrPubKey, string? senderNpub, DateTime? since,
        Action<string, string, string, DateTime> action, Action onAllMessagesReceived);
    
    /// <summary>
    /// Looks up all investment-related messages for a project and categorizes them by type.
    /// Returns requests, notifications, cancellations, and approvals in a single call.
    /// </summary>
    Task LookupAllInvestmentMessagesAsync(
        string nostrPubKey, 
        string? senderNpub, 
        DateTime? since,
        Action<InvestmentMessageType, string, string, string, DateTime> onMessage,
        Action onAllMessagesReceived);
    
    void LookupInvestmentRequestApprovals(string nostrPubKey, Action<string, DateTime, string> action,
        Action onAllMessagesReceived);

    DateTime SendSignaturesToInvestor(string encryptedSignatureInfo, string nostrPrivateKey,
        string investorNostrPubKey, string eventId);

    DateTime SendReleaseSigsToInvestor(string encryptedReleaseSigInfo, string nostrPrivateKeyHex, string investorNostrPubKey, string eventId);

    void LookupReleaseSigs(string investorNostrPubKey, string projectNostrPubKey, DateTime? releaseRequestSentTime, string releaseRequestEventId, Action<string> action, Action onAllMessagesReceived);

    void LookupSignedReleaseSigs(string projectNostrPubKey, Action<SignServiceLookupItem> action, Action onAllMessagesReceived);
}

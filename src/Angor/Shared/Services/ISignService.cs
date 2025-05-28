using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using Nostr.Client.Responses;

namespace Angor.Shared.Services;

public interface ISignService
{
    (DateTime eventTime, string eventId) PostInvestmentRequest(string encryptedContent, string investorNostrPrivateKey, string founderNostrPubKey, Action<NostrOkResponse> okResponse);
    
    void GetInvestmentRequestApproval(string investorNostrPubKey, string projectNostrPubKey, DateTime? sigRequestSentTime, string sigRequestEventId, Func<string, Task> action);

    Task GetAllInvestmentRequests(string nostrPubKey, string? senderNpub, DateTime? since, Action<string, string, string, DateTime> action, Action onAllMessagesReceived);
    
    void GetAllInvestmentRequestApprovals(string nostrPubKey, Action<string, DateTime, string> action, Action onAllMessagesReceived);

    DateTime PostInvestmentRequestApproval(string encryptedSignatureInfo, string nostrPrivateKey, string investorNostrPubKey, string eventId);
    
    DateTime PostInvestmentRevocation(string encryptedReleaseSigInfo, string nostrPrivateKeyHex, string investorNostrPubKey, string eventId);

    void GetInvestmentRevocation(string investorNostrPubKey, string projectNostrPubKey, DateTime? releaseRequestSentTime, string releaseRequestEventId, Action<string> action, Action onAllMessagesReceived);

    void GetAllInvestmentRevocations(string projectNostrPubKey, Action<SignServiceLookupItem> action, Action onAllMessagesReceived);
    Task<Result<EventSendResponse>> PostInvestmentRequest2<T>(KeyIdentifier keyIdentifier, T content, string founderNostrPubKey);
    Task<Result<EventSendResponse>> PostInvestmentRequestApproval2<T>(KeyIdentifier keyIdentifier, T content, string investorNostrPubKey, string eventId);
}

public record EventSendResponse(bool IsAccepted, string? EventId, string? Message, DateTime Received);

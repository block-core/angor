using Angor.Shared.Models;

namespace Angor.Client.Services;

public interface ISignService
{
    (DateTime eventTime, string eventId) RequestInvestmentSigs(SignRecoveryRequest signRecoveryRequest);
    void LookupSignatureForInvestmentRequest(string investorNostrPubKey, string projectNostrPubKey, DateTime sigRequestSentTime, string sigRequestEventId, Func<string, Task> action);

    Task LookupInvestmentRequestsAsync(string nostrPubKey, DateTime? since, Action<string, string, string, DateTime> action,
        Action onAllMessagesReceived);
    
    void LookupInvestmentRequestApprovals(string nostrPubKey, Action<string, DateTime, string> action,
        Action onAllMessagesReceived);

    DateTime SendSignaturesToInvestor(string encryptedSignatureInfo, string nostrPrivateKey,
        string investorNostrPubKey, string eventId);
}
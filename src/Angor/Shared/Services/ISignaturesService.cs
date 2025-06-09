using CSharpFunctionalExtensions;

namespace Angor.Shared.Services;

public interface ISignaturesService
{
    public record EventSendResponse(bool IsAccepted, string? EventId, string? Message, DateTime Received);
    
    Task<Result<EventSendResponse>> PostInvestmentRequest<T>(KeyIdentifier keyIdentifier, T content, string founderNostrPubKey);
    Task<Result<EventSendResponse>> PostInvestmentRequestApproval<T>(KeyIdentifier keyIdentifier, T content, string investorNostrPubKey, string eventId);
}
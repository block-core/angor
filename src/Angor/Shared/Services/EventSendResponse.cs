namespace Angor.Shared.Services;

public record EventSendResponse(bool IsAccepted, string? EventId, string? Message, DateTime Received);
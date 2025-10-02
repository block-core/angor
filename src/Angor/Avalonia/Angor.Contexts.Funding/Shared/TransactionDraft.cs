namespace Angor.Contexts.Funding.Shared;

public record TransactionDraft
{
    public required string SignedTxHex { get; init; }
    public required string TransactionId { get; init; }
    public required int TransactionFeeSatsPerByte { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

}
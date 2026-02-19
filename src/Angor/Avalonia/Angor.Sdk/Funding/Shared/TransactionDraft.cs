using Angor.Sdk.Common;

namespace Angor.Sdk.Funding.Shared;

public record TransactionDraft
{
    public required string SignedTxHex { get; init; }
    public required string TransactionId { get; init; }
    public required Amount TransactionFee { get; init; }
    //public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
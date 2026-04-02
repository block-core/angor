namespace Angor.Sdk.Funding.Founder.Domain;

/// <summary>
/// A single founder project record persisted locally.
/// Tracks which projects the founder has created so we don't need to
/// scan all derived keys via the indexer every time.
/// </summary>
public class FounderProjectRecord
{
    /// <summary>The on-chain project identifier (derived from wallet keys).</summary>
    public required string ProjectIdentifier { get; set; }

    /// <summary>
    /// The transaction ID of the project creation transaction.
    /// Available when the project was created locally; null when discovered via scan.
    /// </summary>
    public string? CreationTransactionId { get; set; }
}

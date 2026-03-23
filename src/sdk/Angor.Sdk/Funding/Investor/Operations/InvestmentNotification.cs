namespace Angor.Sdk.Funding.Investor.Operations;

/// <summary>
/// Notification sent to founder when a below-threshold investment is published directly to the blockchain.
/// Contains only the essential information needed - the founder can fetch full details from the indexer.
/// </summary>
public class InvestmentNotification
{
    /// <summary>
    /// The project identifier this investment is for.
    /// </summary>
    public string ProjectIdentifier { get; set; } = string.Empty;
    
    /// <summary>
    /// The transaction ID of the published investment transaction.
    /// </summary>
    public string TransactionId { get; set; } = string.Empty;
}

/// <summary>
/// Notification sent to founder when an investor cancels their investment request.
/// This is sent unencrypted as it only contains public identifiers.
/// </summary>
public class CancellationNotification
{
    /// <summary>
    /// The project identifier this cancellation is for.
    /// </summary>
    public string ProjectIdentifier { get; set; } = string.Empty;
    
    /// <summary>
    /// The Nostr event ID of the original investment request that is being cancelled.
    /// </summary>
    public string RequestEventId { get; set; } = string.Empty;
}


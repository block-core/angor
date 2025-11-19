using Angor.Data.Documents.Models;

namespace Angor.Contexts.Funding.Shared;

/// <summary>
/// Represents a combined view of investment requests and approvals for a project conversation
/// </summary>
public class InvestmentConversation : BaseDocument
{
    /// <summary>
    /// Composite key: WalletId + ProjectId
    /// </summary>
    public string WalletId { get; set; } = string.Empty;
    
    /// <summary>
    /// The project ID this conversation belongs to
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;
    
    /// <summary>
    /// The Nostr event ID of the investment request message
    /// </summary>
    public string RequestEventId { get; set; } = string.Empty;
    
    /// <summary>
    /// The Nostr public key of the investor
    /// </summary>
    public string InvestorNostrPubKey { get; set; } = string.Empty;
    
    /// <summary>
    /// When the investment request was created
    /// </summary>
    public DateTime RequestCreated { get; set; }
    
    // SignRecoveryRequest properties
    public string? ProjectIdentifier { get; set; }
    public string? InvestmentTransactionHex { get; set; }
    public string? UnfundedReleaseAddress { get; set; }
    public string? UnfundedReleaseKey { get; set; }
    
    // Approval information
    /// <summary>
    /// The Nostr event ID of the approval message (if approved)
    /// </summary>
    public string? ApprovalEventId { get; set; }
    
    /// <summary>
    /// When the approval was created (if approved)
    /// </summary>
    public DateTime? ApprovalCreated { get; set; }
    
    /// <summary>
    /// Status of the investment request
    /// </summary>
    public InvestmentRequestStatus Status { get; set; } = InvestmentRequestStatus.Pending;
    
    /// <summary>
    /// Whether this has been synced from Nostr
    /// </summary>
    public bool IsSynced { get; set; }
}

public enum InvestmentRequestStatus
{
    Pending,
    Approved,
    Invested,
    Rejected
}


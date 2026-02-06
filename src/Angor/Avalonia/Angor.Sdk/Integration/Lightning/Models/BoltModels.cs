namespace Angor.Sdk.Integration.Lightning.Models;

/// <summary>
/// Represents a Bolt Lightning wallet
/// </summary>
public class BoltWallet
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public long BalanceSats { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Represents a Lightning invoice
/// </summary>
public class BoltInvoice
{
    public string Id { get; set; } = string.Empty;
    public string WalletId { get; set; } = string.Empty;
    public string Bolt11 { get; set; } = string.Empty;
    public string PaymentHash { get; set; } = string.Empty;
    public string PaymentSecret { get; set; } = string.Empty;
    public long AmountSats { get; set; }
    public string Memo { get; set; } = string.Empty;
    public BoltPaymentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
}

/// <summary>
/// Represents a Lightning payment
/// </summary>
public class BoltPayment
{
    public string Id { get; set; } = string.Empty;
    public string WalletId { get; set; } = string.Empty;
    public string PaymentHash { get; set; } = string.Empty;
    public string Bolt11 { get; set; } = string.Empty;
    public long AmountSats { get; set; }
    public long FeeSats { get; set; }
    public BoltPaymentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? FailureReason { get; set; }
}

/// <summary>
/// Payment status for Lightning invoices and payments
/// </summary>
public enum BoltPaymentStatus
{
    Pending,
    Paid,
    Expired,
    Failed,
    Cancelled
}

/// <summary>
/// Configuration for Bolt API
/// </summary>
public class BoltConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.bolt.observer";
    public int TimeoutSeconds { get; set; } = 30;
}


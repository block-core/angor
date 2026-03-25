namespace App.UI.Shared;

/// <summary>
/// Canonical signature/funding request status enum.
/// Replaces magic strings "waiting", "approved", "rejected" used throughout:
/// - SignatureRequestViewModel
/// - SharedSignature
/// - FundersViewModel filter logic
/// - SignatureStore
/// </summary>
public enum SignatureStatus
{
    Waiting,
    Approved,
    Rejected
}

public static class SignatureStatusExtensions
{
    /// <summary>Lowercase string for serialization/comparison: "waiting", "approved", "rejected".</summary>
    public static string ToLowerString(this SignatureStatus status) => status switch
    {
        SignatureStatus.Approved => "approved",
        SignatureStatus.Rejected => "rejected",
        _ => "waiting"
    };

    /// <summary>Parse from lowercase string.</summary>
    public static SignatureStatus FromString(string value) => value switch
    {
        "approved" => SignatureStatus.Approved,
        "rejected" => SignatureStatus.Rejected,
        _ => SignatureStatus.Waiting
    };
}

namespace AngorApp.Model.Funded.Shared.Model;

public static class RecoveryActionPolicy
{
    public static RecoveryActionKind Resolve(RecoveryState recoveryState)
    {
        return recoveryState switch
        {
            { HasUnspentItems: true, HasReleaseSignatures: true } => RecoveryActionKind.ClaimReleasedFunds,
            { HasUnspentItems: true, EndOfProject: true } => RecoveryActionKind.ClaimFunds,
            { HasUnspentItems: true, IsAboveThreshold: false } => RecoveryActionKind.ClaimFundsBelowThreshold,
            { HasUnspentItems: true, HasSpendableItemsInPenalty: false } => RecoveryActionKind.RecoverToPenalty,
            { HasSpendableItemsInPenalty: true } => RecoveryActionKind.RecoverFromPenalty,
            _ => RecoveryActionKind.None
        };
    }

    public static string GetButtonLabel(RecoveryState recoveryState)
    {
        return GetButtonLabel(Resolve(recoveryState));
    }

    public static string GetButtonLabel(RecoveryActionKind actionKind)
    {
        return actionKind switch
        {
            RecoveryActionKind.ClaimReleasedFunds => "Recover without Penalty",
            RecoveryActionKind.ClaimFunds => "Recover",
            RecoveryActionKind.ClaimFundsBelowThreshold => "Recover",
            RecoveryActionKind.RecoverToPenalty => "Recover to Penalty",
            RecoveryActionKind.RecoverFromPenalty => "Recover from Penalty",
            _ => string.Empty
        };
    }

    public static string GetDialogTitle(RecoveryActionKind actionKind)
    {
        return actionKind switch
        {
            RecoveryActionKind.ClaimReleasedFunds => "Claim Released Funds",
            RecoveryActionKind.ClaimFunds => "Claim Funds",
            RecoveryActionKind.ClaimFundsBelowThreshold => "Claim Funds (Below Threshold)",
            RecoveryActionKind.RecoverToPenalty => "Recover Funds",
            RecoveryActionKind.RecoverFromPenalty => "Release Funds",
            _ => string.Empty
        };
    }

    public static string GetSuccessMessage(RecoveryActionKind actionKind)
    {
        return actionKind switch
        {
            RecoveryActionKind.ClaimReleasedFunds => "Released funds have been claimed successfully",
            RecoveryActionKind.ClaimFunds => "Funds claim transaction has been submitted successfully",
            RecoveryActionKind.ClaimFundsBelowThreshold => "Funds claim transaction has been submitted successfully",
            RecoveryActionKind.RecoverToPenalty => "Funds recovery transaction has been submitted successfully",
            RecoveryActionKind.RecoverFromPenalty => "Penalty release transaction has been submitted successfully",
            _ => string.Empty
        };
    }
}

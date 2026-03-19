namespace AngorApp.Model.Funded.Shared.Model;

public enum RecoveryActionKind
{
    None,
    ClaimReleasedFunds,
    ClaimFunds,
    ClaimFundsBelowThreshold,
    RecoverToPenalty,
    RecoverFromPenalty
}

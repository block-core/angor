namespace AngorApp.Model.Funded.Shared.Model;

public record RecoveryState(
    bool HasUnspentItems,
    bool HasSpendableItemsInPenalty,
    bool HasReleaseSignatures,
    bool EndOfProject,
    bool IsAboveThreshold)
{
    public static readonly RecoveryState None = new(false, false, false, false, false);
}

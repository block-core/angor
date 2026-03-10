namespace AngorApp.Model.Funded.Shared.Model;

public record RecoveryState(
    bool HasUnspentItems,
    bool HasSpendableItemsInPenalty,
    bool HasReleaseSignatures,
    bool EndOfProject,
    bool IsAboveThreshold)
{
    public static readonly RecoveryState None = new(false, false, false, false, false);

    public string ButtonLabel => this switch
    {
        { HasUnspentItems: true, HasReleaseSignatures: true } => "Recover without Penalty",
        { HasUnspentItems: true, EndOfProject: true } or { HasUnspentItems: true, IsAboveThreshold: false } => "Recover",
        { HasUnspentItems: true, HasSpendableItemsInPenalty: false } => "Recover to Penalty",
        { HasSpendableItemsInPenalty: true } => "Recover from Penalty",
        _ => string.Empty
    };
}

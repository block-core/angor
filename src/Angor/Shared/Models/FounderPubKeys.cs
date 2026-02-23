namespace Angor.Shared.Models;

public class FounderKeyCollection
{
    public List<FounderKeys> Keys { get; set; } = new();
}

public class FounderKeys
{
    public string FounderKey { get; set; } = string.Empty;

    public string FounderRecoveryKey { get; set; } = string.Empty;

    public string ProjectIdentifier { get; set; } = string.Empty;

    public string NostrPubKey { get; set; } = string.Empty;

    public int Index { get; set; }
}
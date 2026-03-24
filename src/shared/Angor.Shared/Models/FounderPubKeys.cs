namespace Angor.Shared.Models;

public class FounderKeyCollection
{
    public List<FounderKeys> Keys { get; set; } = new();
}

public class FounderKeys
{
    public string FounderKey { get; set; }

    public string FounderRecoveryKey { get; set; }

    public string ProjectIdentifier { get; set; }

    public string NostrPubKey { get; set; }

    public int Index { get; set; }
}
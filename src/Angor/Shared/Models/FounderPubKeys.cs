namespace Angor.Shared.Models;

public class FounderKeyCollection
{
    public List<FounderKeys> Keys { get; set; } = new();
}

public class FounderKeys
{
    public string FounderKey { get; set; }

    public string ProjectIdentifier { get; set; }

    public int Index { get; set; }
}
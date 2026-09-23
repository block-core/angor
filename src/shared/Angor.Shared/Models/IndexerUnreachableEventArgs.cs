namespace Angor.Shared.Models;

/// <summary>
/// Raised when the currently selected primary indexer fails to respond to a request.
/// Consumers (e.g. the UI shell) can use this to prompt the user to pick a different
/// configured indexer instead of silently retrying/falling back automatically.
/// </summary>
public class IndexerUnreachableEventArgs : EventArgs
{
    public IndexerUnreachableEventArgs(string indexerUrl, string reason)
    {
        IndexerUrl = indexerUrl;
        Reason = reason;
    }

    /// <summary>The host/URL of the indexer that failed to respond.</summary>
    public string IndexerUrl { get; }

    /// <summary>A short, human-readable description of the failure.</summary>
    public string Reason { get; }
}

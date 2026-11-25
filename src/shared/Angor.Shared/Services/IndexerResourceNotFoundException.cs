namespace Angor.Shared.Services;

/// <summary>
/// The indexer answered 404: the requested transaction/address is unknown. The server itself is healthy.
/// Derives from InvalidOperationException so existing callers keep working.
/// </summary>
public sealed class IndexerResourceNotFoundException(string? message) : InvalidOperationException(message);

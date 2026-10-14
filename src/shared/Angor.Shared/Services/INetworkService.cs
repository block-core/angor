using Angor.Shared.Models;

namespace Angor.Shared.Services;

public interface INetworkService
{
    Task CheckServices(bool force = false);
    void AddSettingsIfNotExist();
    SettingsUrl GetPrimaryIndexer();
    SettingsUrl GetPrimaryExplorer();
    SettingsUrl GetPrimaryRelay();
    List<SettingsUrl> GetRelays();
    List<SettingsUrl> GetDiscoveryRelays();
    SettingsUrl GetPrimaryChatApp();

    void CheckAndHandleError(HttpResponseMessage httpResponseMessage);
    void HandleException(Exception exception);
    void CheckAndSetNetwork(string url, string? setNetwork = null);
    event Action OnStatusChanged;

    /// <summary>
    /// Raised when a request against the currently selected primary indexer fails.
    /// No automatic fallback happens — subscribers (e.g. the UI shell) should surface
    /// this to the user so they can manually pick a different configured indexer.
    /// </summary>
    event EventHandler<IndexerUnreachableEventArgs>? IndexerUnreachable;

    /// <summary>
    /// Marks the given indexer as offline and raises <see cref="IndexerUnreachable"/>.
    /// Debounced per indexer URL to avoid flooding subscribers when many concurrent
    /// requests fail against the same dead indexer.
    /// </summary>
    void NotifyIndexerUnreachable(string indexerUrl, string reason);
}
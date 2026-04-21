using Angor.Shared;
using Angor.Shared.Models;

namespace App.Composition;

/// <summary>
/// <see cref="INetworkStorage"/> decorator that overrides the indexer and relay
/// lists with values from environment variables, without ever persisting them.
///
/// Used by integration tests (see <c>src/design/App.Test.Integration/docker</c>)
/// to point the app at a local docker stack for the lifetime of the process only.
///
/// Reads return the overridden values. Writes pass through to the inner storage
/// but with the overridden lists replaced by whatever the inner currently has,
/// so the persisted settings are never polluted with test-only URLs.
///
///   <c>ANGOR_INDEXER_URL</c>   — replaces the indexer list with this single URL (primary)
///   <c>ANGOR_RELAY_URLS</c>    — comma-separated; replaces the relay list
/// </summary>
public sealed class EnvOverrideNetworkStorage : INetworkStorage
{
    public const string IndexerUrlVariable = "ANGOR_INDEXER_URL";
    public const string RelayUrlsVariable = "ANGOR_RELAY_URLS";

    private readonly INetworkStorage _inner;
    private readonly List<SettingsUrl>? _indexerOverride;
    private readonly List<SettingsUrl>? _relayOverride;

    public EnvOverrideNetworkStorage(INetworkStorage inner)
    {
        _inner = inner;
        _indexerOverride = BuildIndexerOverride();
        _relayOverride = BuildRelayOverride();
    }

    /// <summary>True when at least one override variable is set.</summary>
    public bool HasOverrides => _indexerOverride != null || _relayOverride != null;

    public static bool IsActive() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(IndexerUrlVariable))
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(RelayUrlsVariable));

    public SettingsInfo GetSettings()
    {
        var settings = _inner.GetSettings();

        if (_indexerOverride != null)
        {
            settings.Indexers.Clear();
            settings.Indexers.AddRange(CloneUrls(_indexerOverride));
        }

        if (_relayOverride != null)
        {
            settings.Relays.Clear();
            settings.Relays.AddRange(CloneUrls(_relayOverride));
        }

        return settings;
    }

    public void SetSettings(SettingsInfo settingsInfo)
    {
        // Strip overridden fields back to the inner's current persisted state
        // so test-only URLs never leak into the database.
        if (_indexerOverride != null || _relayOverride != null)
        {
            var persisted = _inner.GetSettings();

            if (_indexerOverride != null)
            {
                settingsInfo.Indexers.Clear();
                settingsInfo.Indexers.AddRange(persisted.Indexers);
            }

            if (_relayOverride != null)
            {
                settingsInfo.Relays.Clear();
                settingsInfo.Relays.AddRange(persisted.Relays);
            }
        }

        _inner.SetSettings(settingsInfo);
    }

    public void SetNetwork(string network) => _inner.SetNetwork(network);

    public string GetNetwork() => _inner.GetNetwork();

    private static List<SettingsUrl>? BuildIndexerOverride()
    {
        var indexerUrl = Environment.GetEnvironmentVariable(IndexerUrlVariable);
        if (string.IsNullOrWhiteSpace(indexerUrl))
        {
            return null;
        }

        return new List<SettingsUrl>
        {
            new() { Name = "local", Url = indexerUrl.Trim(), IsPrimary = true }
        };
    }

    private static List<SettingsUrl>? BuildRelayOverride()
    {
        var relayUrls = Environment.GetEnvironmentVariable(RelayUrlsVariable);
        if (string.IsNullOrWhiteSpace(relayUrls))
        {
            return null;
        }

        var list = new List<SettingsUrl>();
        var first = true;
        foreach (var url in relayUrls.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            list.Add(new SettingsUrl { Name = "local", Url = url, IsPrimary = first });
            first = false;
        }

        return list.Count == 0 ? null : list;
    }

    private static IEnumerable<SettingsUrl> CloneUrls(IEnumerable<SettingsUrl> source) =>
        source.Select(u => new SettingsUrl
        {
            Name = u.Name,
            Url = u.Url,
            IsPrimary = u.IsPrimary,
            Status = u.Status,
            LastCheck = u.LastCheck
        });
}

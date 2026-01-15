using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Angor.Shared.Services.Electrum;

/// <summary>
/// Pool for managing multiple ElectrumClient connections.
/// Handles connection lifecycle, failover, and server selection.
/// </summary>
public class ElectrumClientPool : IDisposable
{
    private readonly ILogger<ElectrumClientPool> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, ElectrumClient> _clients = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly List<ElectrumServerConfig> _serverConfigs;
    private string? _primaryServerKey;
    private bool _disposed;

    public ElectrumClientPool(
        ILogger<ElectrumClientPool> logger,
        ILoggerFactory loggerFactory,
        IEnumerable<ElectrumServerConfig> serverConfigs)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _serverConfigs = serverConfigs.ToList();
    }

    /// <summary>
    /// Gets a connected ElectrumClient, preferring the primary server.
    /// Falls back to other servers if primary is unavailable.
    /// </summary>
    public async Task<ElectrumClient> GetClientAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            // Try primary server first
            if (!string.IsNullOrEmpty(_primaryServerKey) && _clients.TryGetValue(_primaryServerKey, out var primaryClient))
            {
                if (primaryClient.IsConnected)
                    return primaryClient;
            }

            // Try to connect to any server
            foreach (var config in _serverConfigs)
            {
                var key = GetServerKey(config);

                if (_clients.TryGetValue(key, out var existingClient))
                {
                    if (existingClient.IsConnected)
                    {
                        _primaryServerKey = key;
                        return existingClient;
                    }

                    // Clean up disconnected client
                    _clients.TryRemove(key, out _);
                    existingClient.Dispose();
                }

                try
                {
                    var client = await CreateAndConnectClientAsync(config, cancellationToken);
                    _clients[key] = client;
                    _primaryServerKey = key;
                    _logger.LogInformation("Connected to Electrum server {Server}", key);
                    return client;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to connect to Electrum server {Host}:{Port}", config.Host, config.Port);
                }
            }

            throw new ElectrumException("Unable to connect to any Electrum server");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Gets a client for a specific server configuration.
    /// </summary>
    public async Task<ElectrumClient> GetClientForServerAsync(ElectrumServerConfig config, CancellationToken cancellationToken = default)
    {
        var key = GetServerKey(config);

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_clients.TryGetValue(key, out var existingClient) && existingClient.IsConnected)
            {
                return existingClient;
            }

            var client = await CreateAndConnectClientAsync(config, cancellationToken);
            _clients[key] = client;
            return client;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Adds a new server configuration to the pool.
    /// </summary>
    public void AddServer(ElectrumServerConfig config)
    {
        if (!_serverConfigs.Any(c => GetServerKey(c) == GetServerKey(config)))
        {
            _serverConfigs.Add(config);
        }
    }

    /// <summary>
    /// Removes a server from the pool and disconnects any existing connection.
    /// </summary>
    public async Task RemoveServerAsync(string host, int port)
    {
        var key = $"{host}:{port}";

        var config = _serverConfigs.FirstOrDefault(c => GetServerKey(c) == key);
        if (config != null)
        {
            _serverConfigs.Remove(config);
        }

        if (_clients.TryRemove(key, out var client))
        {
            await client.DisconnectAsync();
            client.Dispose();
        }

        if (_primaryServerKey == key)
        {
            _primaryServerKey = null;
        }
    }

    /// <summary>
    /// Sets the primary server. The pool will prefer this server for requests.
    /// </summary>
    public void SetPrimaryServer(string host, int port)
    {
        _primaryServerKey = $"{host}:{port}";
    }

    /// <summary>
    /// Disconnects all clients.
    /// </summary>
    public async Task DisconnectAllAsync()
    {
        foreach (var client in _clients.Values)
        {
            try
            {
                await client.DisconnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disconnecting from Electrum server");
            }
        }

        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }

        _clients.Clear();
        _primaryServerKey = null;
    }

    /// <summary>
    /// Gets the list of configured servers.
    /// </summary>
    public IReadOnlyList<ElectrumServerConfig> GetServers() => _serverConfigs.AsReadOnly();

    /// <summary>
    /// Checks if a specific server is connected.
    /// </summary>
    public bool IsServerConnected(string host, int port)
    {
        var key = $"{host}:{port}";
        return _clients.TryGetValue(key, out var client) && client.IsConnected;
    }

    private async Task<ElectrumClient> CreateAndConnectClientAsync(ElectrumServerConfig config, CancellationToken cancellationToken)
    {
        var clientLogger = _loggerFactory.CreateLogger<ElectrumClient>();
        var client = new ElectrumClient(clientLogger, config);
        await client.ConnectAsync(cancellationToken);
        return client;
    }

    private static string GetServerKey(ElectrumServerConfig config) => $"{config.Host}:{config.Port}";

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }

        _clients.Clear();
        _connectionLock.Dispose();

        GC.SuppressFinalize(this);
    }
}

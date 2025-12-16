using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using System.Net.Http;

namespace Angor.Sdk.Tests.Funding.TestDoubles;

/// <summary>
/// Test implementation of INetworkService for integration tests.
/// Provides a configured indexer URL and basic network service functionality.
/// </summary>
public class TestNetworkService : INetworkService
{
    private readonly SettingsUrl _indexerUrl;
    private readonly INetworkConfiguration _networkConfiguration;

    public TestNetworkService(SettingsUrl indexerUrl, INetworkConfiguration networkConfiguration)
    {
        _indexerUrl = indexerUrl;
        _networkConfiguration = networkConfiguration;
    }

    public Action? OnStatusChanged { get; set; }

    public SettingsUrl GetPrimaryIndexer()
    {
        return _indexerUrl;
    }

    public void CheckAndHandleError(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"HTTP request failed with status code {response.StatusCode}: {response.ReasonPhrase}");
        }
    }

    public void SetNetwork(string network)
    {
        // Not needed for tests - network is already set via NetworkConfiguration
    }

    public string GetNetwork()
    {
        return _networkConfiguration.GetNetwork().Name;
    }

    // Not needed for integration tests - provide minimal implementations
    public SettingsUrl GetPrimaryRelay() => new SettingsUrl { Name = "Test", Url = "wss://test" };
    public List<SettingsUrl> GetRelays() => new List<SettingsUrl>();
    public SettingsUrl GetPrimaryExplorer() => _indexerUrl;
    public SettingsUrl GetPrimaryChatApp() => new SettingsUrl { Name = "Test", Url = "http://test" };
    public List<SettingsUrl> GetDiscoveryRelays() => new List<SettingsUrl>();
    public void AddSettingsIfNotExist() { }
    public void CheckAndSetNetwork(string network, string? genesisHash) { }
    event Action? INetworkService.OnStatusChanged
    {
        add => throw new NotImplementedException();
        remove => throw new NotImplementedException();
    }

    public Task CheckServices(bool checkIndexer) => Task.CompletedTask;
    public void HandleException(Exception ex) { }
}


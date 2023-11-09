using Angor.Shared;
using Microsoft.JSInterop;
using System.Net.WebSockets;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Angor.Client.Storage;

namespace Angor.Client.Services
{
    public interface INetworkService
    {
        Task CheckServices();

        SettingsUrl GetPrimaryIndexer();
    }

    public class NetworkService : INetworkService
    {
        private readonly INetworkStorage _networkStorage;
        private readonly HttpClient _httpClient;
        private readonly ILogger<NetworkService> _logger;

        public NetworkService(INetworkStorage networkStorage, HttpClient httpClient, ILogger<NetworkService> logger)
        {
            _networkStorage = networkStorage;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task CheckServices()
        {
            var settings = _networkStorage.GetSettings();

            foreach (var indexerUrl  in settings.Indexers)
            {
                if ((DateTime.UtcNow - indexerUrl.LastCheck).Minutes > 1)
                {
                    indexerUrl.LastCheck = DateTime.UtcNow;

                    try
                    {
                        var response = await _httpClient.GetAsync($"{indexerUrl}/api/stats/heartbeat");

                        if (response.IsSuccessStatusCode)
                        {
                            indexerUrl.IsOnline = true;
                        }
                        else
                        {
                            _logger.LogError($"Failed to check indexer status url = {indexerUrl.Url}, StatusCode = {response.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        indexerUrl.IsOnline = false;
                        _logger.LogError(ex, $"Failed to check indexer status url = {indexerUrl.Url}");
                    }
                }
            }

            foreach (var relayUrl in settings.Relays)
            {
                if ((DateTime.UtcNow - relayUrl.LastCheck).Minutes > 1)
                {
                    relayUrl.LastCheck = DateTime.UtcNow;

                    try
                    {
                        var uri = new Uri(relayUrl.Url);
                        var response = await _httpClient.GetAsync(uri);

                        if (response.IsSuccessStatusCode)
                        {
                            relayUrl.IsOnline = true;
                        }
                        else
                        {
                            _logger.LogError($"Failed to check relay status url = {relayUrl.Url}, StatusCode = {response.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        relayUrl.IsOnline = false;
                        _logger.LogError(ex, $"Failed to check relay status url = {relayUrl.Url}");
                    }
                }
            }

            _networkStorage.SetSettings(settings);
        }

        public SettingsUrl GetPrimaryIndexer()
        {
            var settings = _networkStorage.GetSettings();

            return settings.Indexers.First(p => p.IsPrimary);
        }
    }
}

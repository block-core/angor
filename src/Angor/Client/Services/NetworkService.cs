using System.Net;
using Angor.Shared;
using Microsoft.JSInterop;
using System.Net.WebSockets;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Angor.Client.Storage;

namespace Angor.Client.Services
{
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

        public async Task CheckServices(bool force = false)
        {
            var settings = _networkStorage.GetSettings();

            foreach (var indexerUrl  in settings.Indexers)
            {
                if (force || (DateTime.UtcNow - indexerUrl.LastCheck).Minutes > 10)
                {
                    indexerUrl.LastCheck = DateTime.UtcNow;

                    try
                    {
                        var uri = new Uri(indexerUrl.Url);
                        var response = await _httpClient.GetAsync($"{uri}api/stats/heartbeat");

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
                if (force || (DateTime.UtcNow - relayUrl.LastCheck).Minutes > 1)
                {
                    relayUrl.LastCheck = DateTime.UtcNow;

                    try
                    {
                        var uri = new Uri(relayUrl.Url);
                        var httpUri = uri.Scheme == "wss" ? new Uri($"https://{uri.Host}/") : new Uri($"http://{uri.Host}/");
                        var response = await _httpClient.GetAsync(httpUri);

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

            var ret = settings.Indexers.First(p => p.IsPrimary);

            return ret;
        }

        public SettingsUrl GetPrimaryRelay()
        {
            var settings = _networkStorage.GetSettings();

            return settings.Relays.First(p => p.IsPrimary);
        }

        public List<SettingsUrl> GetRelays()
        {
            var settings = _networkStorage.GetSettings();

            return settings.Relays;
        }

        public void CheckAndHandleError(HttpResponseMessage httpResponseMessage)
        {
            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                if (httpResponseMessage.StatusCode == HttpStatusCode.NotFound)
                {
                    var settings = _networkStorage.GetSettings();

                    var host = settings.Indexers.FirstOrDefault(a => new Uri(a.Url).Host == httpResponseMessage.RequestMessage?.RequestUri?.Host);

                    if (host != null)
                    {
                        host.IsOnline = false;
                        _networkStorage.SetSettings(settings);
                    }
                }
            }
        }
    }
}

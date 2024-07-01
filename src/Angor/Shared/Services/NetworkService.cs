using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Blockcore.Networks;
using Microsoft.Extensions.Logging;

namespace Angor.Shared.Services
{
    public class NetworkService : INetworkService
    {
        private readonly INetworkStorage _networkStorage;
        private readonly HttpClient _httpClient;
        private readonly ILogger<NetworkService> _logger;
        private readonly INetworkConfiguration _networkConfiguration;
        public event Action OnStatusChanged;


        public NetworkService(INetworkStorage networkStorage, HttpClient httpClient, ILogger<NetworkService> logger, INetworkConfiguration networkConfiguration)
        {
            _networkStorage = networkStorage;
            _httpClient = httpClient;
            _logger = logger;
            _networkConfiguration = networkConfiguration;
        }

        public void CheckAndSetNetwork(string url, string? setNetwork = null)
        {
            string networkName = _networkStorage.GetNetwork();
            if (string.IsNullOrEmpty(networkName))
            {
                Network network = null;

                if (setNetwork != null)
                {
                    network = setNetwork.Contains("test") ? new BitcoinTest() : new BitcoinMain();

                } else if (url.Contains("test"))
                {
                    network = new BitcoinTest();
                }
                else if (url.Contains("localhost"))
                {
                    network = new BitcoinTest();
                }
                else
                {
                    network = new BitcoinMain();
                }

                _networkStorage.SetNetwork(network.NetworkType.ToString());
                _networkConfiguration.SetNetwork(network);
            }
            else
            {
                if (networkName == NetworkType.Mainnet.ToString())
                {
                    _networkConfiguration.SetNetwork(new BitcoinMain());
                }

                if (networkName == NetworkType.Testnet.ToString())
                {
                    _networkConfiguration.SetNetwork(new BitcoinTest());
                }
            }
        }

        public void AddSettingsIfNotExist()
        {
            var settings = _networkStorage.GetSettings();

            if (!settings.Indexers.Any())
            {
                settings.Indexers.AddRange(_networkConfiguration.GetDefaultIndexerUrls());
                _networkStorage.SetSettings(settings);
            }

            if (!settings.Relays.Any())
            {
                settings.Relays.AddRange(_networkConfiguration.GetDefaultRelayUrls());
                _networkStorage.SetSettings(settings);
            }
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
                            indexerUrl.Status = UrlStatus.Online;
                        }
                        else
                        {
                            _logger.LogError($"Failed to check indexer status url = {indexerUrl.Url}, StatusCode = {response.StatusCode}");
                        }
                        OnStatusChanged?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        indexerUrl.Status = UrlStatus.Offline;
                        _logger.LogError(ex, $"Failed to check indexer status url = {indexerUrl.Url}");
                    }
                }
            }

            var nostrHeaderMediaType = new MediaTypeWithQualityHeaderValue("application/nostr+json");
            _httpClient.DefaultRequestHeaders.Accept.Add(nostrHeaderMediaType);
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
                            relayUrl.Status = UrlStatus.Online;
                            var relayInfo = await response.Content.ReadFromJsonAsync<NostrRelayInfo>();
                            relayUrl.Name = relayInfo?.Name ?? string.Empty;
                        }
                        else
                        {
                            _logger.LogError($"Failed to check relay status url = {relayUrl.Url}, StatusCode = {response.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        relayUrl.Status = UrlStatus.Offline;
                        _logger.LogError(ex, $"Failed to check relay status url = {relayUrl.Url}");
                    }
                }
            }

            _httpClient.DefaultRequestHeaders.Accept.Remove(nostrHeaderMediaType);
            _networkStorage.SetSettings(settings);
        }

        public SettingsUrl GetPrimaryIndexer()
        {
            var settings = _networkStorage.GetSettings();

            var ret = settings.Indexers.FirstOrDefault(p => p.IsPrimary);

            if (ret == null)
            {
                throw new ApplicationException("No indexer found. The indexer appears to be offline. To resolve this issue, please go to the settings and add or refresh the indexer.");
            }

            return ret;
        }

        public SettingsUrl GetPrimaryRelay()
        {
            var settings = _networkStorage.GetSettings();

            var ret = settings.Relays.FirstOrDefault(p => p.IsPrimary);

            if (ret == null)
            {
                throw new ApplicationException("No relay found go to settings to add a relay.");
            }

            return ret;

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
                        host.Status = UrlStatus.Offline;
                        _networkStorage.SetSettings(settings);
                    }
                }
            }
        }

        public void HandleException(Exception exception)
        {
            if (exception is HttpRequestException httpRequestException)
            {
                // dont block the caller
                Task.Run(async () =>
                {
                    // this code will run outside of the UI thread, however wasm is single threaded so it will block the UI, 
                    // we should consider using a cancellation token in this case that will not cancel after a shorter time to block the UI for too long 

                    await CheckServices(true);
                });
            }
        }
    }
}

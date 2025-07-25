﻿using System.Net;
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
        private readonly IHttpClientFactory _clientFactory;
        private readonly ILogger<NetworkService> _logger;
        private readonly INetworkConfiguration _networkConfiguration;
        public event Action OnStatusChanged;


        public NetworkService(INetworkStorage networkStorage, IHttpClientFactory clientFactory, ILogger<NetworkService> logger, INetworkConfiguration networkConfiguration)
        {
            _networkStorage = networkStorage;
            _clientFactory = clientFactory;
            _logger = logger;
            _networkConfiguration = networkConfiguration;
        }

        /// <summary>
        /// This method will read the current network from storage and set it in config
        /// If no network found in storage it will look at the property 'setNetwork' to determine what network to set in config and in storage
        /// If the 'setNetwork' is null then we look at the url for hints as to what network to initiate.
        /// </summary>
        public void CheckAndSetNetwork(string url, string? setNetwork = null)
        {
            string networkName = _networkStorage.GetNetwork();

            if (!string.IsNullOrEmpty(networkName))
            {
                // if the network is specified in storage
                // we create set it in the configuration

                _networkConfiguration.SetNetwork(AngorNetworksSelector.NetworkByName(networkName));
            }
            else
            {
                // no network found ether this is a first
                // time user visits the site or the network was wiped

                Network network = null;

                if (setNetwork != null)
                {
                    network = AngorNetworksSelector.NetworkByName(setNetwork);
                } 
                else if (url.Contains("test") || url.Contains("debug") || url.Contains("localhost"))
                {
                    network = new Angornet();
                }
                else
                {
                    network = new BitcoinMain();
                }

                _networkStorage.SetNetwork(network.Name);
                _networkConfiguration.SetNetwork(network);
            }
        }

        public void AddSettingsIfNotExist()
        {
            var settings = _networkStorage.GetSettings();

            if (!settings.Explorers.Any())
            {
                settings.Explorers.AddRange(_networkConfiguration.GetDefaultExplorerUrls());
            }

            if (!settings.Indexers.Any())
            {
                settings.Indexers.AddRange(_networkConfiguration.GetDefaultIndexerUrls());
            }

            if (!settings.Relays.Any())
            {
                settings.Relays.AddRange(_networkConfiguration.GetDefaultRelayUrls());
            }

            if (!settings.ChatApps.Any())
            {
                settings.ChatApps.AddRange(_networkConfiguration.GetDefaultChatAppUrls());
            }

            _networkStorage.SetSettings(settings);
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
                        
                        var blockUrl = Path.Combine(uri.AbsoluteUri, "api", "v1", "block-height", "0");

                        var response = await _clientFactory.CreateClient()
                            .GetAsync(blockUrl);

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

            //foreach (var explorerUrl in settings.Explorers)
            //{
            //    if (force || (DateTime.UtcNow - explorerUrl.LastCheck).Minutes > 10)
            //    {
            //        explorerUrl.LastCheck = DateTime.UtcNow;

            //        try
            //        {
            //            var uri = new Uri(explorerUrl.Url);
                        
            //             var heartbeatUrl = new Uri(uri, "api/stats/heartbeat").ToString();
            //             var response = await _httpClient.GetAsync(heartbeatUrl);

            //            if (response.IsSuccessStatusCode)
            //            {
            //                explorerUrl.Status = UrlStatus.Online;
            //            }
            //            else
            //            {
            //                _logger.LogError($"Failed to check explorer status url = {explorerUrl.Url}, StatusCode = {response.StatusCode}");
            //            }
            //            OnStatusChanged?.Invoke();
            //        }
            //        catch (Exception ex)
            //        {
            //            explorerUrl.Status = UrlStatus.Offline;
            //            _logger.LogError(ex, $"Failed to check explorer status url = {explorerUrl.Url}");
            //        }
            //    }
            //}

            var nostrHeaderMediaType = new MediaTypeWithQualityHeaderValue("application/nostr+json");
            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Accept.Add(nostrHeaderMediaType);
            foreach (var relayUrl in settings.Relays)
            {
                if (force || (DateTime.UtcNow - relayUrl.LastCheck).Minutes > 1)
                {
                    relayUrl.LastCheck = DateTime.UtcNow;

                    try
                    {
                        var uri = new Uri(relayUrl.Url);
                        var httpUri = uri.Scheme == "wss"
                            ? new Uri($"https://{uri.Host}/")
                            : new Uri($"http://{uri.Host}/");

                        var response = await client.GetAsync(httpUri);

                        if (response.IsSuccessStatusCode)
                        {
                            relayUrl.Status = UrlStatus.Online;
                            var relayInfo = await response.Content.ReadFromJsonAsync<NostrRelayInfo>();
                            relayUrl.Name = relayInfo?.Name ?? string.Empty;
                        }
                        else
                        {
                            _logger.LogError(
                                $"Failed to check relay status url = {relayUrl.Url}, StatusCode = {response.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        relayUrl.Status = UrlStatus.Offline;
                        _logger.LogError(ex, $"Failed to check relay status url = {relayUrl.Url}");
                    }
                }
            }

            client.DefaultRequestHeaders.Accept.Remove(nostrHeaderMediaType);
            _networkStorage.SetSettings(settings);
        }

        public SettingsUrl GetPrimaryIndexer()
        {
            var settings = _networkStorage.GetSettings();

            var ret = settings.Indexers.FirstOrDefault(p => p.IsPrimary);

            if (ret == null)
            {
                throw new ApplicationException("No indexer found go to settings to add an indexer.");
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

        public List<SettingsUrl> GetDiscoveryRelays()
        {
            return _networkConfiguration.GetDiscoveryRelays();
        }

        public SettingsUrl GetPrimaryExplorer()
        {
            var settings = _networkStorage.GetSettings();

            var ret = settings.Explorers.FirstOrDefault(p => p.IsPrimary);

            if (ret == null)
            {
                throw new ApplicationException("No explorer found go to settings to add an explorer.");
            }

            return ret;
        }

        public SettingsUrl GetPrimaryChatApp()
        {
            var settings = _networkStorage.GetSettings();

            var ret = settings.ChatApps.FirstOrDefault(p => p.IsPrimary);

            if (ret == null)
            {
                throw new ApplicationException("No chat application found go to settings to add a chat application.");
            }

            return ret;
        }

        public void CheckAndHandleError(HttpResponseMessage httpResponseMessage)
        {
            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                if (httpResponseMessage.StatusCode == HttpStatusCode.NotFound)
                {
                    var settings = _networkStorage.GetSettings();

                    var host = settings.Indexers.FirstOrDefault(a =>
                        new Uri(a.Url).Host == httpResponseMessage.RequestMessage?.RequestUri?.Host);

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
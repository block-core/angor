using System.Collections.Concurrent;
using System.Reactive.Linq;
using ConcurrentCollections;
using Microsoft.Extensions.Logging;
using Nostr.Client.Client;
using Nostr.Client.Communicator;

namespace Angor.Shared.Services;

public class NostrCommunicationFactory : IDisposable , INostrCommunicationFactory
{
    private readonly ILogger<NostrWebsocketClient> _clientLogger; 
    private readonly ILogger<NostrCommunicationFactory> _logger;

    private NostrMultiWebsocketClient? _nostrMultiWebsocketClient;
    private NostrMultiWebsocketClient? _nostrMultiWebsocketClientDiscovery;
    private readonly List<IDisposable> _serviceSubscriptions;

    private ConcurrentDictionary<string, ConcurrentHashSet<string>> _eoseCalledOnSubscriptionClients;
    private ConcurrentDictionary<string, ConcurrentHashSet<string>> _okCalledOnSubscriptionClients;

    public NostrCommunicationFactory(ILogger<NostrWebsocketClient> clientLogger, ILogger<NostrCommunicationFactory> logger)
    {
        _clientLogger = clientLogger;
        _logger = logger;
        _serviceSubscriptions = new();
        _eoseCalledOnSubscriptionClients = new();
        _okCalledOnSubscriptionClients = new();
    }

    private ConcurrentHashSet<string> GetAllConnectedRelayNames()
    {
        var allRelays = new ConcurrentHashSet<string>();

        if (_nostrMultiWebsocketClient != null)
        {
            foreach (var client in _nostrMultiWebsocketClient.Clients)
            {
                allRelays.Add(client.Communicator.Name);
            }
        }

        // if (_nostrMultiWebsocketClientDiscovery != null)
        // {
        //     foreach (var client in _nostrMultiWebsocketClientDiscovery.Clients)
        //     {
        //         allRelays.Add(client.Communicator.Name);
        //     }
        // }

        return allRelays;
    }
    
    
    public INostrClient GetOrCreateDiscoveryClients(INetworkService networkService)
    {
        if (_nostrMultiWebsocketClientDiscovery == null)
        {
            _nostrMultiWebsocketClientDiscovery = new NostrMultiWebsocketClient(_clientLogger);
        }

        foreach (var url in networkService.GetDiscoveryRelays()
                     .Where(url => _nostrMultiWebsocketClientDiscovery.FindClient(url.Name) == null))
        {
            var communicator = CreateCommunicator(url.Url, url.Name);
            var client = new NostrWebsocketClient(communicator, _clientLogger);

            _serviceSubscriptions.Add(client.Streams.EoseStream.Subscribe(x =>
            {
                _logger.LogDebug($"{x.CommunicatorName} EOSE {x.Subscription}");
                if (!_eoseCalledOnSubscriptionClients.TryGetValue(x.Subscription ?? string.Empty,
                        out var clientsReceivedList)) return;
                
                var tryRemove = clientsReceivedList.TryRemove(x.CommunicatorName);
                    
                _logger.LogWarning("EOSE {x.Subscription} removed {x.CommunicatorName} - {tryRemove}",
                    x.Subscription, x.CommunicatorName, tryRemove);
            }));

            _serviceSubscriptions.Add(client.Streams.OkStream.Subscribe(x =>
            {
                _logger.LogDebug($"{x.CommunicatorName} OK {x.EventId} {x.Accepted}");
                if (_okCalledOnSubscriptionClients.TryGetValue(x.EventId ?? string.Empty, out var clientsReceivedList))
                {
                    var tryRemove = clientsReceivedList.TryRemove(x.CommunicatorName);
                    _logger.LogWarning($"OK {x.EventId} accepted: {x.Accepted} removed ok {x.CommunicatorName} - {tryRemove}");
                }
            }));

            _nostrMultiWebsocketClientDiscovery!.RegisterClient(client);

            communicator.StartOrFail();
        }

        return _nostrMultiWebsocketClientDiscovery;
    }

    public INostrClient GetOrCreateClient(INetworkService networkService)
      {
        if (_nostrMultiWebsocketClient is not null)
        {
            ConnectToAllRelaysInTheSettings(networkService);
            
            return _nostrMultiWebsocketClient;
        }
        
        _nostrMultiWebsocketClient = new NostrMultiWebsocketClient(_clientLogger);

        ConnectToAllRelaysInTheSettings(networkService);

        // if (_logger.IsEnabled(LogLevel.Debug))
        // {
            _serviceSubscriptions.Add(_nostrMultiWebsocketClient.Streams.UnknownMessageStream.Subscribe(_ =>
                _logger.LogError($"UnknownMessageStream {_.MessageType} {_.AdditionalData}")));

            _serviceSubscriptions.Add(_nostrMultiWebsocketClient.Streams.EventStream
                .Where(_ => _.Event?.AdditionalData?.Any() ?? false).Subscribe(_ =>
                    _logger.LogDebug(
                        $"EventStream {_.Subscription} {_.Event?.Id} {_.Event?.AdditionalData}")));

            _serviceSubscriptions.Add(_nostrMultiWebsocketClient.Streams.NoticeStream.Subscribe(_ =>
                _logger.LogError($"NoticeStream {_.Message}")));

            _serviceSubscriptions.Add(_nostrMultiWebsocketClient.Streams.UnknownRawStream.Subscribe(_ =>
                _logger.LogError($"UnknownRawStream {_.Message}")));
        //}

        return _nostrMultiWebsocketClient;
    }

    private void ConnectToAllRelaysInTheSettings(INetworkService networkService)
    {
        foreach (var url in networkService.GetRelays()
                     .Where(url => _nostrMultiWebsocketClient.FindClient(url.Name) == null))
        {
            var communicator = CreateCommunicator(url.Url, url.Name);
            var client = new NostrWebsocketClient(communicator, _clientLogger);
            
            _serviceSubscriptions.Add(client.Streams.EoseStream.Subscribe(x =>
            {
                _logger.LogDebug($"{x.CommunicatorName} EOSE {x.Subscription}");
                if (!_eoseCalledOnSubscriptionClients.TryGetValue(x.Subscription ?? string.Empty,
                        out var clientsReceivedList)) return;
                _logger.LogDebug($"EOSE {x.Subscription} adding {x.CommunicatorName}");
                     
                var tryRemove = clientsReceivedList.TryRemove(x.CommunicatorName);
                    
                _logger.LogWarning("EOSE {x.Subscription} removed {x.CommunicatorName} - {tryRemove}",
                    x.Subscription, x.CommunicatorName, tryRemove);
            }));
            
            _serviceSubscriptions.Add(client.Streams.OkStream.Subscribe(x =>
            {
                _logger.LogDebug($"{x.CommunicatorName} OK {x.EventId} {x.Accepted}");
                if (_okCalledOnSubscriptionClients.TryGetValue(x.EventId ?? string.Empty, out var clientsReceivedList))
                {
                    var tryRemove = clientsReceivedList.TryRemove(x.CommunicatorName);
                    _logger.LogWarning($"OK {x.EventId} accepted: {x.Accepted} removed ok {x.CommunicatorName} - {tryRemove}");
                } 
            }));
            
            _nostrMultiWebsocketClient!.RegisterClient(client);
            
            communicator.StartOrFail();
        }
    }

    public void CloseClientConnection()
    {
        Dispose();
    }

    public bool EoseEventReceivedOnAllRelays(string subscription)
    {
        if (!_eoseCalledOnSubscriptionClients.ContainsKey(subscription))
            return true; //If not monitoring than no need to block

        _logger.LogDebug($"Checking for all Eose on monitored subscription {subscription}");

        var response = _eoseCalledOnSubscriptionClients[subscription].IsEmpty;
        
        _logger.LogDebug($"Eose on monitored subscription {subscription} received from all clients - {response}");
        
        return response;
    }
    
    public bool MonitoringEoseReceivedOnSubscription(string subscription)
    {
        _logger.LogDebug($"Started monitoring subscription {subscription}");
        return _eoseCalledOnSubscriptionClients.TryAdd(subscription, GetAllConnectedRelayNames());
    }
    
    public void ClearEoseReceivedOnSubscriptionMonitoring(string subscription)
    {
        _logger.LogDebug($"Stopped monitoring subscription {subscription}");
        _eoseCalledOnSubscriptionClients.Remove(subscription, out _);
    }
    
    public bool OkEventReceivedOnAllRelays(string eventId)
    {
        if (!_okCalledOnSubscriptionClients.ContainsKey(eventId))
            return true; //If not monitoring than no need to block

        _logger.LogDebug($"Checking for all Ok on monitored subscription {eventId}");
        
        bool response = _okCalledOnSubscriptionClients[eventId].IsEmpty;
        
        _logger.LogDebug($"Eose on monitored subscription {eventId} received from all clients - {response}");

        return response;
    }
    
    public void MonitoringOkReceivedOnSubscription(string eventId)
    {
        _logger.LogDebug($"Started monitoring event id {eventId}");
        _okCalledOnSubscriptionClients.TryAdd(eventId, GetAllConnectedRelayNames());
    }
    
    public void ClearOkReceivedOnSubscriptionMonitoring(string eventId)
    {
        _logger.LogDebug($"Started monitoring event id {eventId}");
        _okCalledOnSubscriptionClients.Remove(eventId, out _);
    }
    
    public int GetNumberOfRelaysConnected()
    {
        return _nostrMultiWebsocketClient?.Clients.Count ?? 0;
    }

    public INostrCommunicator CreateCommunicator(string uri, string relayName)
    {
        var nostrCommunicator = new NostrWebsocketCommunicator(new Uri(uri))
        {
            Name = relayName,
            ReconnectTimeout = null //TODO need to check what is the actual best time to set here
        };

        _serviceSubscriptions.Add(nostrCommunicator.DisconnectionHappened.Subscribe(e =>
        {
            if (e.Exception != null)
                _logger.LogError(e.Exception,
                    "Relay {relayName} disconnected, type: {Type}, reason: {CloseStatusDescription}", 
                    relayName, e.Type, e.CloseStatusDescription);
            else
                _logger.LogDebug(
                    "Relay {relayName} disconnected, type: {Type}, reason: {CloseStatusDescription}", 
                    relayName, e.Type, e.CloseStatusDescription);
        }));

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _serviceSubscriptions.Add(nostrCommunicator.MessageReceived.Subscribe(e =>
            {
                _logger.LogDebug(
                    "message received on communicator {relayName} - {Text} Relay message received, type: {MessageType}",
                    relayName, e.Text, e.MessageType);
            }));
        }

        return nostrCommunicator;
    }

    public void Dispose()
    {
        _serviceSubscriptions.ForEach(subscription => subscription.Dispose());
        _serviceSubscriptions.Clear();
        _nostrMultiWebsocketClient?.Dispose();
        _nostrMultiWebsocketClient = null;
        _eoseCalledOnSubscriptionClients = new();
    }
}
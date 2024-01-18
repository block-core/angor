using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using Nostr.Client.Client;
using Nostr.Client.Communicator;

namespace Angor.Shared.Services;

public class NostrCommunicationFactory : IDisposable , INostrCommunicationFactory
{
    private readonly ILogger<NostrWebsocketClient> _clientLogger; 
    private readonly ILogger<NostrWebsocketCommunicator> _communicatorLogger;

    private NostrMultiWebsocketClient? _nostrMultiWebsocketClient;
    private readonly List<IDisposable> _serviceSubscriptions;

    private Dictionary<string, List<string>> EoseCalledOnSubscriptionClients;

    public NostrCommunicationFactory(ILogger<NostrWebsocketClient> clientLogger, ILogger<NostrWebsocketCommunicator> communicatorLogger)
    {
        _clientLogger = clientLogger;
        _communicatorLogger = communicatorLogger;
        _serviceSubscriptions = new();
        EoseCalledOnSubscriptionClients = new();
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

        _serviceSubscriptions.Add(_nostrMultiWebsocketClient.Streams.UnknownMessageStream.Subscribe(_ =>
            _clientLogger.LogError($"UnknownMessageStream {_.MessageType} {_.AdditionalData}")));
        
        _serviceSubscriptions.Add(_nostrMultiWebsocketClient.Streams.EventStream
            .Where(_ => _.Event?.AdditionalData?.Any() ?? false).Subscribe(_ =>
                _clientLogger.LogInformation(
                    $"EventStream {_.Subscription} {_.Event?.Id} {_.Event?.AdditionalData}")));
        
        _serviceSubscriptions.Add(_nostrMultiWebsocketClient.Streams.NoticeStream.Subscribe(_ =>
                _clientLogger.LogError($"NoticeStream {_.Message}")));
        
        _serviceSubscriptions.Add(_nostrMultiWebsocketClient.Streams.UnknownRawStream.Subscribe(_ =>
                _clientLogger.LogError($"UnknownRawStream {_.Message}")));

        return _nostrMultiWebsocketClient;
    }

    private void ConnectToAllRelaysInTheSettings(INetworkService networkService)
    {
        foreach (var url in networkService.GetRelays()
                     .Where(url => _nostrMultiWebsocketClient.FindClient(url.Name) == null))
        {
            var communicator = CreateCommunicator(url.Url, url.Name);
            var client = new NostrWebsocketClient(communicator, _clientLogger);
            
            _serviceSubscriptions.Add(client.Streams.EoseStream.Subscribe(_ =>
            {
                if (EoseCalledOnSubscriptionClients.ContainsKey(_.Subscription))
                    EoseCalledOnSubscriptionClients[_.Subscription].Add(_.CommunicatorName); //TODO 
            }));
            
            _nostrMultiWebsocketClient!.RegisterClient(client);
            
            communicator.StartOrFail();
        }
    }

    public void CloseClientConnection()
    {
        Dispose();
    }

    public bool EventReceivedOnAllRelays(string subscription)
    {
        if (!EoseCalledOnSubscriptionClients.ContainsKey(subscription))
            return true; //If not monitoring than no need to block
        
        return _nostrMultiWebsocketClient?.Clients
            .All(x => 
                EoseCalledOnSubscriptionClients[subscription].Contains(x.Communicator.Name)) ?? false;
    }
    
    public void MonitoringEoseReceivedOnSubscription(string subscription)
    {
        EoseCalledOnSubscriptionClients.Add(subscription, new List<string>());
    }
    
    public void ClearEoseReceivedOnSubscriptionMonitoring(string subscription)
        {
        EoseCalledOnSubscriptionClients.Remove(subscription);
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
                _communicatorLogger.LogError(e.Exception,
                    "Relay {relayName} disconnected, type: {Type}, reason: {CloseStatusDescription}", 
                    relayName, e.Type, e.CloseStatusDescription);
            else
                _communicatorLogger.LogInformation(
                    "Relay {relayName} disconnected, type: {Type}, reason: {CloseStatusDescription}", 
                    relayName, e.Type, e.CloseStatusDescription);
        }));

        _serviceSubscriptions.Add(nostrCommunicator.MessageReceived.Subscribe(e =>
        {
            _communicatorLogger.LogInformation(
                "message received on communicator {relayName} - {Text} Relay message received, type: {MessageType}",
                relayName, e.Text, e.MessageType);
        }));
        
        return nostrCommunicator;
    }

    public void Dispose()
    {
        _serviceSubscriptions.ForEach(subscription => subscription.Dispose());
        _serviceSubscriptions.Clear();
        _nostrMultiWebsocketClient?.Dispose();
        _nostrMultiWebsocketClient = null;
        EoseCalledOnSubscriptionClients = new();
    }
}
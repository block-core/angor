using Microsoft.Extensions.Logging;
using Nostr.Client.Client;
using Nostr.Client.Communicator;

namespace Angor.Shared.Services;

public interface INostrCommunicationFactory
{
    INostrClient CreateClient(INostrCommunicator communicator);
    INostrCommunicator CreateCommunicator(string uri, string relayName);
}

class NostrCommunicationFactory : INostrCommunicationFactory
{
    private ILogger<NostrWebsocketClient> _clientLogger; 
    private ILogger<NostrWebsocketCommunicator> _communicatorLogger;

    public NostrCommunicationFactory(ILogger<NostrWebsocketClient> clientLogger, ILogger<NostrWebsocketCommunicator> communicatorLogger)
    {
        _clientLogger = clientLogger;
        _communicatorLogger = communicatorLogger;
    }

    public INostrClient CreateClient(INostrCommunicator communicator)
    {
        var nostrClient = new NostrWebsocketClient(communicator, _clientLogger);
            
        nostrClient.Streams.UnknownMessageStream.Subscribe(_ => _clientLogger.LogError($"UnknownMessageStream {_.MessageType} {_.AdditionalData}"));
        nostrClient.Streams.EventStream.Subscribe(_ => _clientLogger.LogInformation($"EventStream {_.Subscription} {_.AdditionalData}"));
        nostrClient.Streams.NoticeStream.Subscribe(_ => _clientLogger.LogError($"NoticeStream {_.Message}"));
        nostrClient.Streams.UnknownRawStream.Subscribe(_ => _clientLogger.LogError($"UnknownRawStream {_.Message}"));
            
        nostrClient.Streams.OkStream.Subscribe(_ =>
        {
            _clientLogger.LogInformation($"OkStream {_.Accepted} message - {_.Message}");

            // if (_.EventId != null && OkVerificationActions.ContainsKey(_.EventId))
            // {
            //     OkVerificationActions[_.EventId](_);
            //     OkVerificationActions.Remove(_.EventId);
            // }
        });

        nostrClient.Streams.EoseStream.Subscribe(_ =>
        {
            _clientLogger.LogInformation($"EoseStream {_.Subscription} message - {_.AdditionalData}");
                
            // if (!subscriptions.ContainsKey(_.Subscription))
            //     return;

            nostrClient.Streams.EventStream.Subscribe(_ => { }, _ => { },() => {_clientLogger.LogInformation("Event stream closed");});
            
            // _clientLogger.LogInformation($"Disposing of subscription - {_.Subscription}");
            // subscriptions[_.Subscription].Dispose();
            // subscriptions.Remove(_.Subscription);
            // _clientLogger.LogInformation($"subscription disposed - {_.Subscription}");
        });

        return nostrClient;
    }

    public INostrCommunicator CreateCommunicator(string uri, string relayName)
    {
        var nostrCommunicator = new NostrWebsocketCommunicator(new Uri(uri))
        {
            Name = relayName,
            ReconnectTimeout = null //TODO need to check what is the actual best time to set here
        };

        nostrCommunicator.DisconnectionHappened.Subscribe(_ =>
        {
            if (_.Exception != null)
                _communicatorLogger.LogError(_.Exception,
                    "Relay {RelayName} disconnected, type: {Type}, reason: {CloseStatusDescription}", 
                    relayName, _.Type, _.CloseStatusDescription);
            else
                _communicatorLogger.LogInformation(
                    "Relay {RelayName} disconnected, type: {Type}, reason: {CloseStatusDescription}", 
                    relayName, _.Type, _.CloseStatusDescription);
        });

        nostrCommunicator.MessageReceived.Subscribe(_ =>
        {
            _communicatorLogger.LogInformation(
                "message received on communicator {RelayName} - {Text} Relay message received, type: {MessageType}",
                relayName, _.Text, _.MessageType);
        });
        
        return nostrCommunicator;
    }
}
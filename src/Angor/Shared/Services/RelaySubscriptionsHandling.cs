using System.ComponentModel;
using Blockcore.EventBus;
using Microsoft.Extensions.Logging;
using Nostr.Client.Requests;
using Nostr.Client.Responses;

namespace Angor.Shared.Services;

public class RelaySubscriptionsHandling : IDisposable, IRelaySubscriptionsHandling
{
    private ILogger<RelaySubscriptionsHandling> _logger;
    protected Dictionary<string, IDisposable> relaySubscriptions;
    protected Dictionary<string, Action> userEoseActions;
    protected Dictionary<string, Action<NostrOkResponse>> OkVerificationActions;

    protected Dictionary<string, string> relaySubscriptionsKeepActive;

    private INostrCommunicationFactory _communicationFactory;
    private INetworkService _networkService;

    private IDisposable _okHandlingSubscription;
    private IDisposable _eoseHandlingSubscription;

    public RelaySubscriptionsHandling(ILogger<RelaySubscriptionsHandling> logger, INostrCommunicationFactory communicationFactory, INetworkService networkService)
    {
        _logger = logger;
        _communicationFactory = communicationFactory;
        _networkService = networkService;
        relaySubscriptions = new();
        userEoseActions = new();
        OkVerificationActions = new();
        relaySubscriptionsKeepActive = new();

        var client = _communicationFactory.GetOrCreateClient(networkService); 
        
        _okHandlingSubscription = client.Streams.OkStream.Subscribe(HandleOkMessages);
        _eoseHandlingSubscription = client.Streams.EoseStream.Subscribe(HandleEoseMessages);
    }

    // public void Init(INetworkService networkService)
    // {
    //     var client = _communicationFactory.GetOrCreateClient(networkService); 
    //     
    //     _okHandlingSubscription = client.Streams.OkStream.Subscribe(HandleOkMessages);
    //     _eoseHandlingSubscription = client.Streams.EoseStream.Subscribe(HandleEoseMessages);
    // }
    
    public bool TryAddOKAction(string eventId, Action<NostrOkResponse> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        _communicationFactory.MonitoringOkReceivedOnSubscription(eventId);
        return OkVerificationActions.TryAdd(eventId,action);
    }

    public void HandleOkMessages(NostrOkResponse okResponse)
    {
        _logger.LogDebug($"OkStream {okResponse.Accepted} message - {okResponse.Message}");

        if (!OkVerificationActions.TryGetValue(okResponse?.EventId ?? string.Empty, out var action)) 
            return;
        
        action(okResponse);

        if (!_communicationFactory.OkEventReceivedOnAllRelays(okResponse.EventId)) 
            return;
        
        OkVerificationActions.Remove(okResponse.EventId ?? string.Empty);
        _communicationFactory.ClearOkReceivedOnSubscriptionMonitoring(okResponse.EventId);
    }

    public bool TryAddEoseAction(string subscriptionName, Action action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        var add = _communicationFactory.MonitoringEoseReceivedOnSubscription(subscriptionName);

        if (!add)
            _logger.LogDebug($"Subscription {subscriptionName} is already being monitored");

        return userEoseActions.TryAdd(subscriptionName,action); 
    }

    public void HandleEoseMessages(NostrEoseResponse _)
    {
        _logger.LogDebug($"EoseStream {_.Subscription} message - {_.AdditionalData}");

        if (!_communicationFactory.EoseEventReceivedOnAllRelays(_.Subscription))
            return;
        
        if (userEoseActions.TryGetValue(_.Subscription, out var action))
        {
            _logger.LogDebug($"Invoking action on EOSE - {_.Subscription}");
            try
            {
                action.Invoke();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to invoke end of events event action");
            }

            userEoseActions.Remove(_.Subscription);
            _logger.LogDebug($"Removed action on EOSE for subscription - {_.Subscription}");
        }

        _communicationFactory.ClearEoseReceivedOnSubscriptionMonitoring(_.Subscription);
        
        if (!relaySubscriptions.ContainsKey(_.Subscription)) 
            return;

        if (relaySubscriptionsKeepActive.ContainsKey(_.Subscription))
            return;

        CloseSubscription(_.Subscription);
    }

    public void CloseSubscription(string subscriptionKey)
    {
        if (!relaySubscriptions.ContainsKey(subscriptionKey))
            return;
        
        _communicationFactory
            .GetOrCreateClient(_networkService)
            .Send(new NostrCloseRequest(subscriptionKey));
       
        relaySubscriptions[subscriptionKey].Dispose();
        relaySubscriptions.Remove(subscriptionKey);
        
        _logger.LogDebug($"subscription disposed - {subscriptionKey}");
    }

    public bool RelaySubscriptionAdded(string subscriptionKey)
    {
        return relaySubscriptions.ContainsKey(subscriptionKey);
    }

    public bool TryAddRelaySubscription(string subscriptionKey, IDisposable subscription, bool keepActive = false)
    {
        if (relaySubscriptions.ContainsKey(subscriptionKey))
            return true;

        if (relaySubscriptions.TryAdd(subscriptionKey, subscription))
        {
            if (keepActive)
            {
                relaySubscriptionsKeepActive.TryAdd(subscriptionKey, string.Empty);
            }

            return true;
        }

        return false;
    }

    public void Dispose()
    {
        relaySubscriptions.Values.ToList().ForEach(_ => _.Dispose());
        _okHandlingSubscription.Dispose();
        _eoseHandlingSubscription.Dispose();
        _communicationFactory.CloseClientConnection();
    }
}   
using System.Collections.Concurrent;
using System.ComponentModel;
using Blockcore.EventBus;
using Microsoft.Extensions.Logging;
using Nostr.Client.Requests;
using Nostr.Client.Responses;

namespace Angor.Shared.Services;

public class RelaySubscriptionsHandling : IDisposable, IRelaySubscriptionsHandling
{
    private ILogger<RelaySubscriptionsHandling> _logger;
    protected ConcurrentDictionary<string, IDisposable> relaySubscriptions;
    protected ConcurrentDictionary<string, Action> userEoseActions;
    protected ConcurrentDictionary<string, Action<NostrOkResponse>> OkVerificationActions;

    protected ConcurrentDictionary<string, string> relaySubscriptionsKeepActive;

    private INostrCommunicationFactory _communicationFactory;
    private INetworkService _networkService;

    private IDisposable _okHandlingSubscription;
    private IDisposable _eoseHandlingSubscription;
    
    private IDisposable _okHandlingDiscoverySubscription;
    private IDisposable _eoseHandlingDiscoverySubscription;

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
        
        var discoveryClient = _communicationFactory.GetOrCreateDiscoveryClients(networkService); 
        
        _okHandlingDiscoverySubscription = discoveryClient.Streams.OkStream.Subscribe(HandleOkMessages);
        _eoseHandlingDiscoverySubscription = discoveryClient.Streams.EoseStream.Subscribe(HandleEoseMessages);

        _communicationFactory.RelayDisconnected += OnRelayDisconnected;
    }

    private void OnRelayDisconnected(string relayName)
    {
        // After a relay disconnects and is removed from EOSE/OK tracking sets,
        // re-evaluate all pending actions — some may now be satisfied.
        foreach (var subscription in userEoseActions.Keys.ToList())
        {
            if (_communicationFactory.EoseEventReceivedOnAllRelays(subscription))
            {
                if (userEoseActions.Remove(subscription, out var action))
                {
                    _logger.LogWarning(
                        "Relay {RelayName} disconnect unblocked EOSE action for subscription {Subscription}",
                        relayName, subscription);
                    try
                    {
                        action.Invoke();
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to invoke EOSE action after relay disconnect");
                    }
                }

                _communicationFactory.ClearEoseReceivedOnSubscriptionMonitoring(subscription);
            }
        }

        foreach (var eventId in OkVerificationActions.Keys.ToList())
        {
            if (_communicationFactory.OkEventReceivedOnAllRelays(eventId))
            {
                if (OkVerificationActions.Remove(eventId, out var action))
                {
                    _logger.LogWarning(
                        "Relay {RelayName} disconnect unblocked OK action for event {EventId}",
                        relayName, eventId);
                    try
                    {
                        action.Invoke(new NostrOkResponse { Accepted = true, EventId = eventId });
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to invoke OK action after relay disconnect");
                    }
                }

                _communicationFactory.ClearOkReceivedOnSubscriptionMonitoring(eventId);
            }
        }
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
        
        OkVerificationActions.Remove(okResponse.EventId ?? string.Empty, out _);
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
        
        if (userEoseActions.Remove(_.Subscription, out var action))
        {
            _logger.LogDebug($"Removed action on EOSE for subscription - {_.Subscription}");
            try
            {
                _logger.LogDebug($"Invoking action on EOSE - {_.Subscription}");
                action.Invoke();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to invoke end of events event action");
            }
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
        if (!relaySubscriptions.TryRemove(subscriptionKey, out var subscription))
            return;
        
        _communicationFactory
            .GetOrCreateClient(_networkService)
            .Send(new NostrCloseRequest(subscriptionKey));
       
        subscription.Dispose();
        relaySubscriptionsKeepActive.Remove(subscriptionKey, out _);

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
        _communicationFactory.RelayDisconnected -= OnRelayDisconnected;
        relaySubscriptions.Values.ToList().ForEach(_ => _.Dispose());
        _okHandlingSubscription.Dispose();
        _eoseHandlingSubscription.Dispose();
        _okHandlingDiscoverySubscription.Dispose();
        _eoseHandlingDiscoverySubscription.Dispose();
        _communicationFactory.CloseClientConnection();
    }
}   
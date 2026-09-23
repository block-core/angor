using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using Nostr.Client.Requests;
using Nostr.Client.Responses;

namespace Angor.Shared.Services;

public class RelaySubscriptionsHandling : IDisposable, IRelaySubscriptionsHandling
{
    /// <summary>
    /// Default deadline for a subscription to receive EOSE from all relays before it is
    /// force-closed regardless of relay behaviour. Kept in line with the ~30s timeouts used by
    /// callers such as <c>BuildRecoveryTransaction.LookupFounderSignatures</c>, with some margin.
    /// </summary>
    public static readonly TimeSpan DefaultSubscriptionTimeout = TimeSpan.FromSeconds(45);

    private ILogger<RelaySubscriptionsHandling> _logger;
    protected ConcurrentDictionary<string, IDisposable> relaySubscriptions;
    protected ConcurrentDictionary<string, Action> userEoseActions;
    protected ConcurrentDictionary<string, Action<NostrOkResponse>> OkVerificationActions;

    protected ConcurrentDictionary<string, string> relaySubscriptionsKeepActive;

    /// <summary>
    /// Per-subscription timers that force-close a subscription after <see cref="_subscriptionTimeout"/>
    /// has elapsed, independent of whether every relay has sent EOSE. Subscriptions registered with
    /// <c>keepActive: true</c> are intentionally long-lived (e.g. an ongoing DM listener) and are not
    /// scheduled for timeout-based cleanup.
    /// </summary>
    protected ConcurrentDictionary<string, Timer> subscriptionTimeoutTimers;

    private readonly TimeSpan _subscriptionTimeout;

    private INostrCommunicationFactory _communicationFactory;
    private INetworkService _networkService;

    private IDisposable _okHandlingSubscription;
    private IDisposable _eoseHandlingSubscription;
    
    private IDisposable _okHandlingDiscoverySubscription;
    private IDisposable _eoseHandlingDiscoverySubscription;

    public RelaySubscriptionsHandling(ILogger<RelaySubscriptionsHandling> logger, INostrCommunicationFactory communicationFactory, INetworkService networkService)
        : this(logger, communicationFactory, networkService, DefaultSubscriptionTimeout)
    {
    }

    public RelaySubscriptionsHandling(ILogger<RelaySubscriptionsHandling> logger, INostrCommunicationFactory communicationFactory, INetworkService networkService, TimeSpan subscriptionTimeout)
    {
        _logger = logger;
        _communicationFactory = communicationFactory;
        _networkService = networkService;
        _subscriptionTimeout = subscriptionTimeout;
        relaySubscriptions = new();
        userEoseActions = new();
        OkVerificationActions = new();
        relaySubscriptionsKeepActive = new();
        subscriptionTimeoutTimers = new();

        var client = _communicationFactory.GetOrCreateClient(networkService); 
        
        // Synchronize(): OkStream/EoseStream are fed by every connected relay concurrently
        // (one shared, unsynchronized Rx subject per multi-relay client — see RelayService.cs).
        // Without this, HandleOkMessages/HandleEoseMessages (and every user action they invoke,
        // e.g. TaskCompletionSource.SetResult calls that aren't using the Try-variant) can be
        // re-entered concurrently when two relays respond around the same instant.
        _okHandlingSubscription = client.Streams.OkStream.Synchronize().Subscribe(HandleOkMessages);
        _eoseHandlingSubscription = client.Streams.EoseStream.Synchronize().Subscribe(HandleEoseMessages);
        
        var discoveryClient = _communicationFactory.GetOrCreateDiscoveryClients(networkService); 
        
        _okHandlingDiscoverySubscription = discoveryClient.Streams.OkStream.Synchronize().Subscribe(HandleOkMessages);
        _eoseHandlingDiscoverySubscription = discoveryClient.Streams.EoseStream.Synchronize().Subscribe(HandleEoseMessages);

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

    public bool TryAddEoseAction(string subscriptionName, Action action, bool includeDiscoveryRelays = false)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        var add = _communicationFactory.MonitoringEoseReceivedOnSubscription(subscriptionName, includeDiscoveryRelays);

        if (!add)
            _logger.LogDebug($"Subscription {subscriptionName} is already being monitored");

        // Replace any existing action so repeated refreshes get the current callback.
        userEoseActions[subscriptionName] = action;
        return true;
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
        CancelSubscriptionTimeout(subscriptionKey);

        if (!relaySubscriptions.TryRemove(subscriptionKey, out var subscription))
            return;
        
        _communicationFactory
            .GetOrCreateClient(_networkService)
            .Send(new NostrCloseRequest(subscriptionKey));

        // Some lookups (e.g. NIP-65 relay lists) also send the REQ to the discovery relays;
        // closing there too is harmless when the subscription was never opened on them.
        _communicationFactory
            .GetOrCreateDiscoveryClients(_networkService)
            .Send(new NostrCloseRequest(subscriptionKey));
       
        subscription.Dispose();
        relaySubscriptionsKeepActive.Remove(subscriptionKey, out _);

        _logger.LogDebug($"subscription disposed - {subscriptionKey}");
    }

    /// <summary>
    /// Disposes only the local event stream handler for a subscription key
    /// without sending a Nostr CLOSE to the relay. Use this when you intend
    /// to immediately re-send a REQ with the same subscription key.
    /// </summary>
    public void DisposeLocalSubscription(string subscriptionKey)
    {
        CancelSubscriptionTimeout(subscriptionKey);

        if (relaySubscriptions.TryRemove(subscriptionKey, out var subscription))
        {
            subscription.Dispose();
            _logger.LogDebug($"Local subscription handler disposed (no CLOSE sent) - {subscriptionKey}");
        }
    }

    /// <summary>
    /// Schedules a one-shot timer that force-closes the given subscription after
    /// <see cref="_subscriptionTimeout"/> if it has not already been closed (e.g. via EOSE from
    /// all relays, or an explicit caller-initiated close). This guards against a single relay
    /// that never sends EOSE (stalls silently, or errors without a clean disconnect) leaving the
    /// subscription - and its associated EOSE action - registered indefinitely.
    /// </summary>
    private void ScheduleSubscriptionTimeout(string subscriptionKey)
    {
        // Replace any pre-existing timer for this key so we never leak one.
        if (subscriptionTimeoutTimers.TryRemove(subscriptionKey, out var previousTimer))
            previousTimer.Dispose();

        var timer = new Timer(state =>
        {
            if (!relaySubscriptions.ContainsKey(subscriptionKey))
                return;

            _logger.LogWarning(
                "Subscription {SubscriptionKey} timed out after {Timeout} without EOSE from all relays; forcing cleanup",
                subscriptionKey, _subscriptionTimeout);

            userEoseActions.TryRemove(subscriptionKey, out _);
            _communicationFactory.ClearEoseReceivedOnSubscriptionMonitoring(subscriptionKey);
            CloseSubscription(subscriptionKey);
        }, null, _subscriptionTimeout, Timeout.InfiniteTimeSpan);

        subscriptionTimeoutTimers[subscriptionKey] = timer;
    }

    private void CancelSubscriptionTimeout(string subscriptionKey)
    {
        if (subscriptionTimeoutTimers.TryRemove(subscriptionKey, out var timer))
            timer.Dispose();
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
            else
            {
                // keepActive subscriptions are intentionally long-lived (e.g. an ongoing DM
                // listener) and are exempt from timeout-based cleanup.
                ScheduleSubscriptionTimeout(subscriptionKey);
            }

            return true;
        }

        return false;
    }

    public void Dispose()
    {
        _communicationFactory.RelayDisconnected -= OnRelayDisconnected;
        relaySubscriptions.Values.ToList().ForEach(_ => _.Dispose());
        subscriptionTimeoutTimers.Values.ToList().ForEach(timer => timer.Dispose());
        subscriptionTimeoutTimers.Clear();
        _okHandlingSubscription.Dispose();
        _eoseHandlingSubscription.Dispose();
        _okHandlingDiscoverySubscription.Dispose();
        _eoseHandlingDiscoverySubscription.Dispose();
        _communicationFactory.CloseClientConnection();
    }
}   
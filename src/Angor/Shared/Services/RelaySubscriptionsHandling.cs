using Microsoft.Extensions.Logging;
using Nostr.Client.Requests;
using Nostr.Client.Responses;

namespace Angor.Shared.Services;

public class RelaySubscriptionsHandling : IDisposable, IRelaySubscriptionsHandling
{
    private ILogger<RelaySubscriptionsHandling> _logger;
    protected Dictionary<string, IDisposable> relaySubscriptions;
    protected Dictionary<string, Action> userEoseActions;
    protected Dictionary<string, SubscriptionCallCounter<Action<NostrOkResponse>>> OkVerificationActions;
    
    private INostrCommunicationFactory _communicationFactory;
    private INetworkService _networkService;

    public RelaySubscriptionsHandling(ILogger<RelaySubscriptionsHandling> logger, INostrCommunicationFactory communicationFactory, INetworkService networkService)
    {
        _logger = logger;
        _communicationFactory = communicationFactory;
        _networkService = networkService;
        relaySubscriptions = new();
        userEoseActions = new();
        OkVerificationActions = new();
    }
    
    protected class SubscriptionCallCounter<T>
    {
        public SubscriptionCallCounter(T item)
        {
            Item = item;
        }

        public int NumberOfInvocations { get; set; }
        public T Item { get; }
    }

    public bool TryAddOKAction(string eventId, Action<NostrOkResponse> action)
    {
        return OkVerificationActions.TryAdd(eventId,new SubscriptionCallCounter<Action<NostrOkResponse>>(action));
    }

    public void HandleOkMessages(NostrOkResponse _)
    {
        _logger.LogInformation($"OkStream {_.Accepted} message - {_.Message}");

        if (OkVerificationActions.TryGetValue(_?.EventId ?? string.Empty, out var value))
        {
            value.NumberOfInvocations++;
            value.Item(_);
            if (value.NumberOfInvocations == _communicationFactory.GetNumberOfRelaysConnected())
            {
                OkVerificationActions.Remove(_.EventId ?? string.Empty);
            }
        }
    }

    public bool TryAddEoseAction(string subscriptionName, Action action)
    {
        _communicationFactory.MonitoringEoseReceivedOnSubscription(subscriptionName);
        
        return userEoseActions.TryAdd(subscriptionName,action);
    }

    public void HandleEoseMessages(NostrEoseResponse _)
    {
        _logger.LogInformation($"EoseStream {_.Subscription} message - {_.AdditionalData}");

        if (!_communicationFactory.EventReceivedOnAllRelays(_.Subscription))
            return;
        
        if (userEoseActions.TryGetValue(_.Subscription, out var action))
        {
            _logger.LogInformation($"Invoking action on EOSE - {_.Subscription}");
            try
            {
                action.Invoke();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to invoke end of events event action");
            }

            userEoseActions.Remove(_.Subscription);
            _logger.LogInformation($"Removed action on EOSE for subscription - {_.Subscription}");
        }

        _communicationFactory.ClearEoseReceivedOnSubscriptionMonitoring(_.Subscription);
        
        if (!relaySubscriptions.ContainsKey(_.Subscription)) 
            return;
        
        _logger.LogInformation($"Disposing of subscription - {_.Subscription}");
        
        _communicationFactory
            .GetOrCreateClient(_networkService)
            .Send(new NostrCloseRequest(_.Subscription));
        
        relaySubscriptions[_.Subscription].Dispose();
        relaySubscriptions.Remove(_.Subscription);
        _logger.LogInformation($"subscription disposed - {_.Subscription}");
    }

    public bool RelaySubscriptionAdded(string subscriptionKey)
    {
        return relaySubscriptions.ContainsKey(subscriptionKey);
    }

    public bool TryAddRelaySubscription(string subscriptionKey, IDisposable subscription)
    {
        return relaySubscriptions.ContainsKey(subscriptionKey) || relaySubscriptions.TryAdd(subscriptionKey, subscription);
    }

    public void Dispose()
    {
        relaySubscriptions.Values.ToList().ForEach(_ => _.Dispose());
        _communicationFactory.CloseClientConnection();
    }
}   
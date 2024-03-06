using Microsoft.Extensions.Logging;
using Nostr.Client.Requests;
using Nostr.Client.Responses;

namespace Angor.Shared.Services;

public class RelaySubscriptionsHandling : IDisposable, IRelaySubscriptionsHandling
{
    private ILogger<RelaySubscriptionsHandling> _logger;
    protected Dictionary<string, IDisposable> RelaySubscriptions;
    protected Dictionary<string, Action> UserEoseActions;
    protected Dictionary<string, Action<NostrOkResponse>> OkVerificationActions;
    
    private INostrCommunicationFactory _communicationFactory;
    private INetworkService _networkService;

    private IDisposable _okHandlingSubscription;
    private IDisposable _eoseHandlingSubscription;

    public RelaySubscriptionsHandling(ILogger<RelaySubscriptionsHandling> logger, INostrCommunicationFactory communicationFactory, INetworkService networkService)
    {
        _logger = logger;
        _communicationFactory = communicationFactory;
        _networkService = networkService;
        RelaySubscriptions = new();
        UserEoseActions = new();
        OkVerificationActions = new();
        
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
    
    public bool TryAddOkAction(string eventId, Action<NostrOkResponse> action)
    {
        _communicationFactory.MonitoringOkReceivedOnSubscription(eventId);
        return OkVerificationActions.TryAdd(eventId,action);
    }

    public void HandleOkMessages(NostrOkResponse okResponse)
    {
        _logger.LogInformation($"OkStream {okResponse.Accepted} message - {okResponse.Message}");

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
        _communicationFactory.MonitoringEoseReceivedOnSubscription(subscriptionName);
        
        return UserEoseActions.TryAdd(subscriptionName,action);
    }

    public void HandleEoseMessages(NostrEoseResponse _)
    {
        _logger.LogInformation($"EoseStream {_.Subscription} message - {_.AdditionalData}");

        if (!_communicationFactory.EoseEventReceivedOnAllRelays(_.Subscription))
            return;
        
        if (UserEoseActions.TryGetValue(_.Subscription, out var action))
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

            UserEoseActions.Remove(_.Subscription);
            _logger.LogInformation($"Removed action on EOSE for subscription - {_.Subscription}");
        }

        _communicationFactory.ClearEoseReceivedOnSubscriptionMonitoring(_.Subscription);
        
        if (!RelaySubscriptions.ContainsKey(_.Subscription)) 
            return;
        
        _logger.LogInformation($"Disposing of subscription - {_.Subscription}");
        
        _communicationFactory
            .GetOrCreateClient(_networkService)
            .Send(new NostrCloseRequest(_.Subscription));
        
        RelaySubscriptions[_.Subscription].Dispose();
        RelaySubscriptions.Remove(_.Subscription);
        _logger.LogInformation($"subscription disposed - {_.Subscription}");
    }

    public bool RelaySubscriptionAdded(string subscriptionKey)
    {
        return RelaySubscriptions.ContainsKey(subscriptionKey);
    }

    public bool TryAddRelaySubscription(string subscriptionKey, IDisposable subscription)
    {
        return RelaySubscriptions.ContainsKey(subscriptionKey) || RelaySubscriptions.TryAdd(subscriptionKey, subscription);
    }

    public void Dispose()
    {
        RelaySubscriptions.Values.ToList().ForEach(_ => _.Dispose());
        _okHandlingSubscription.Dispose();
        _eoseHandlingSubscription.Dispose();
        _communicationFactory.CloseClientConnection();
    }
}   
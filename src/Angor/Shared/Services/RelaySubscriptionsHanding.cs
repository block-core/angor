using Microsoft.Extensions.Logging;
using Nostr.Client.Requests;
using Nostr.Client.Responses;

namespace Angor.Shared.Services;

public class RelaySubscriptionsHanding : IDisposable
{
    private ILogger<RelaySubscriptionsHanding> _logger;
    protected Dictionary<string, SubscriptionCallCounter<IDisposable>> userSubscriptions;
    protected Dictionary<string, SubscriptionCallCounter<Action>> userEoseActions;
    protected Dictionary<string, SubscriptionCallCounter<Action<NostrOkResponse>>> OkVerificationActions;
    
    private INostrCommunicationFactory _communicationFactory;
    private INetworkService _networkService;

    protected RelaySubscriptionsHanding(ILogger<RelaySubscriptionsHanding> logger, INostrCommunicationFactory communicationFactory, INetworkService networkService)
    {
        _logger = logger;
        _communicationFactory = communicationFactory;
        _networkService = networkService;
        userSubscriptions = new();
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


    public void HandleOkMessages(NostrOkResponse _)
    {
        _logger.LogInformation($"OkStream {_.Accepted} message - {_.Message}");

        if (OkVerificationActions.TryGetValue(_?.EventId ?? string.Empty, out SubscriptionCallCounter<Action<NostrOkResponse>> value))
        {
            value.NumberOfInvocations++;
            value.Item(_);
            if (value.NumberOfInvocations == _communicationFactory.GetNumberOfRelaysConnected())
            {
                OkVerificationActions.Remove(_.EventId ?? string.Empty);
            }
        }
    }
    
    public void HandleEoseMessages(NostrEoseResponse _)
    {
        _logger.LogInformation($"EoseStream {_.Subscription} message - {_.AdditionalData}");

        if (userEoseActions.TryGetValue(_.Subscription, out SubscriptionCallCounter<Action> value))
        {
            value.NumberOfInvocations++;
            if (userEoseActions[_.Subscription].NumberOfInvocations == _communicationFactory.GetNumberOfRelaysConnected())
            {
                _logger.LogInformation($"Invoking action on EOSE - {_.Subscription}");
                try
                {
                    value.Item.Invoke();
                }
                catch (Exception e)
                {
                    _logger.LogError(e,"Failed to invoke end of events event action");
                }
                userEoseActions.Remove(_.Subscription);
                _logger.LogInformation($"Removed action on EOSE for subscription - {_.Subscription}");   
            }
        }

        if (!userSubscriptions.ContainsKey(_.Subscription)) 
            return;

        userSubscriptions[_.Subscription].NumberOfInvocations++;
                
        if (userSubscriptions[_.Subscription].NumberOfInvocations != _communicationFactory.GetNumberOfRelaysConnected()) 
            return;
                
        _logger.LogInformation($"Disposing of subscription - {_.Subscription}");
        _communicationFactory
            .GetOrCreateClient(_networkService)
            .Send(new NostrCloseRequest(_.Subscription));
        userSubscriptions[_.Subscription].Item.Dispose();
        userSubscriptions.Remove(_.Subscription);
        _logger.LogInformation($"subscription disposed - {_.Subscription}");
    }

    public void Dispose()
    {
        userSubscriptions.Values.ToList().ForEach(_ => _.Item.Dispose());
        _communicationFactory.CloseClientConnection();
    }
}   
using System.Reactive.Disposables;
using Microsoft.Extensions.Logging;
using Nostr.Client.Client;
using Nostr.Client.Communicator;
using Nostr.Client.Messages;
using Nostr.Client.Requests;
using Nostr.Client.Responses;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Angor.Contexts.Data.Services;

public class NostrClientWrapper : INostrClientWrapper, IDisposable
{
    private readonly ILogger<NostrClientWrapper> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly List<string> _relayUrls;
    private readonly List<INostrCommunicator> _communicators;
    private readonly Dictionary<string, IDisposable> _activeSubscriptions;
    private NostrMultiWebsocketClient? _client;
    private bool _isConnected = false;
    private int _connectedRelaysCount = 0;

    private readonly string[] _defaultRelays = {
        "wss://relay.damus.io",
        "wss://nos.lol", 
        "wss://relay.nostr.band"
    };

    public NostrClientWrapper(ILogger<NostrClientWrapper> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _relayUrls = new List<string>(_defaultRelays);
        _communicators = new List<INostrCommunicator>();
        _activeSubscriptions = new Dictionary<string, IDisposable>();
    }

    public bool IsConnected => _isConnected;
    public int ConnectedRelaysCount => _connectedRelaysCount;

    public void AddRelay(string relayUrl)
    {
        if (!_relayUrls.Contains(relayUrl))
        {
            _relayUrls.Add(relayUrl);
            _logger.LogInformation("Added relay: {Relay} (reconnection required)", relayUrl);
            
            // Mark as disconnected to force reconnection with new relay
            if (_isConnected)
            {
                _isConnected = false;
                _logger.LogInformation("Connection state reset - reconnection required to include new relay");
            }
        }
    }

    public void RemoveRelay(string relayUrl)
    {
        if (_relayUrls.Remove(relayUrl))
        {
            _logger.LogInformation("Removed relay: {Relay} (reconnection required)", relayUrl);
            
            // Mark as disconnected to force reconnection without removed relay
            if (_isConnected)
            {
                _isConnected = false;
                _logger.LogInformation("Connection state reset - reconnection required to remove relay");
            }
        }
    }

    public List<string> GetConfiguredRelays()
    {
        return new List<string>(_relayUrls);
    }

    public async Task ConnectAsync()
    {
        if (_isConnected && _client != null)
        {
            _logger.LogDebug("Already connected to relays");
            return;
        }

        _logger.LogInformation("Connecting to {Count} relays...", _relayUrls.Count);
        
        try
        {
            // Dispose existing connections if any
            Disconnect();

            // Create communicators for each relay
            _communicators.Clear();
            foreach (var relayUrl in _relayUrls)
            {
                try
                {
                    var communicator = new NostrWebsocketCommunicator(new Uri(relayUrl));
                    _communicators.Add(communicator);
                    _logger.LogDebug("Added communicator for relay: {Relay}", relayUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create communicator for relay: {Relay}", relayUrl);
                }
            }

            if (_communicators.Count == 0)
            {
                throw new InvalidOperationException("No valid relay communicators could be created");
            }

            // Start all communicators
            var startTasks = _communicators.Select(async communicator =>
            {
                try
                {
                    await communicator.StartOrFail();
                    _logger.LogDebug("Successfully started communicator for relay");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start communicator");
                    return false;
                }
            });

            var results = await Task.WhenAll(startTasks);
            _connectedRelaysCount = results.Count(r => r);
            
            if (_connectedRelaysCount == 0)
            {
                throw new InvalidOperationException("No relay connections could be established");
            }

            // Create multi-client with logger factory using CreateLogger
            _client = new NostrMultiWebsocketClient(_loggerFactory.CreateLogger<NostrWebsocketClient>(), _communicators.ToArray());

            // Setup connection event handlers
            _client.Streams.UnknownMessageStream.Subscribe(msg =>
            {
                _logger.LogDebug("Unknown message received: {Message}", msg);
            });

            _isConnected = true;
            
            _logger.LogInformation("Successfully connected to {SuccessfulCount}/{TotalCount} relays", 
                _connectedRelaysCount, _communicators.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to relays");
            _isConnected = false;
            _connectedRelaysCount = 0;
            throw;
        }
    }

    public IObservable<NostrEventResponse> SubscribeToEvents(NostrKind kind, params string[] eventIds)
    {
        if (_client == null || !_isConnected)
        {
            _logger.LogError("Nostr client is not connected");
            return Observable.Empty<NostrEventResponse>();
        }

        var subscriptionId = $"sub-{kind}-{Guid.NewGuid():N}";
        _logger.LogDebug("Creating subscription {SubscriptionId} for events of kind {Kind}", subscriptionId, kind);

        var eventObservable = _client.Streams.EventStream
            .Where(response => response?.Event?.Kind == kind)
            .Do(response => _logger.LogDebug("Received event {EventId} of kind {Kind}", response.Event?.Id, kind));

        // Create filter - cast NostrKind to int for the filter
        var filter = new NostrFilter
        {
            Kinds = new[] { kind },
            Ids = eventIds?.Length > 0 ? eventIds : null
        };

        // Send subscription request
        _client.Send(new NostrRequest(subscriptionId, filter));

        // Create a subject that will complete when EOSE is received
        var subject = new Subject<NostrEventResponse>();
        
        // Subscribe to events
        var eventSubscription = eventObservable.Subscribe(
            onNext: subject.OnNext,
            onError: subject.OnError
        );

        // Subscribe to EOSE to complete the observable
        var eoseSubscription = _client.Streams.EoseStream
            .Where(eose => eose.Subscription == subscriptionId)
            .Take(1)
            .Subscribe(_ =>
            {
                _logger.LogDebug("End of stored events received for subscription: {SubscriptionId}", subscriptionId);
                subject.OnCompleted();
            });

        // Subscribe to notices for this subscription
        var noticeSubscription = _client.Streams.NoticeStream.Subscribe(notice =>
        {
            _logger.LogWarning("Notice received for subscription {SubscriptionId}: {Message}", subscriptionId, notice.Message);
        });

        // Combine all subscriptions
        var compositeSubscription = new CompositeDisposable(eventSubscription, eoseSubscription, noticeSubscription);
        
        // Store the subscription for cleanup
        _activeSubscriptions[subscriptionId] = compositeSubscription;

        // Return observable that cleans up on disposal
        return subject.AsObservable().Finally(() =>
        {
            compositeSubscription.Dispose();
            _activeSubscriptions.Remove(subscriptionId);
            
            // Send close request
            if (_client != null && _isConnected)
            {
                _client.Send(new NostrCloseRequest(subscriptionId));
                _logger.LogDebug("Closed subscription: {SubscriptionId}", subscriptionId);
            }
        });
    }

    public async Task CloseSubscriptionAsync(string subscriptionId)
    {
        if (_client != null && _isConnected)
        {
            _client.Send(new NostrCloseRequest(subscriptionId));
            _logger.LogDebug("Sent close request for subscription: {SubscriptionId}", subscriptionId);
        }

        if (_activeSubscriptions.TryGetValue(subscriptionId, out var subscription))
        {
            subscription.Dispose();
            _activeSubscriptions.Remove(subscriptionId);
            _logger.LogDebug("Disposed subscription: {SubscriptionId}", subscriptionId);
        }
    }

    public void Disconnect()
    {
        if (_client != null || _communicators.Count > 0)
        {
            _logger.LogInformation("Disconnecting from relays...");
            
            try
            {
                // Close all active subscriptions
                foreach (var subscription in _activeSubscriptions.Values)
                {
                    subscription.Dispose();
                }
                _activeSubscriptions.Clear();

                // Dispose client
                _client?.Dispose();
                _client = null;

                // Dispose all communicators
                foreach (var communicator in _communicators)
                {
                    try
                    {
                        communicator?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error disposing communicator");
                    }
                }
                
                _communicators.Clear();
                _isConnected = false;
                _connectedRelaysCount = 0;
                
                _logger.LogInformation("Disconnected from all relays");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting from relays");
            }
        }
    }

    public void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }
}
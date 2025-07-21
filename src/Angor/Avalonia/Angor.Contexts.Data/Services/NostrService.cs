
using Microsoft.Extensions.Logging;
using Nostr.Client.Client;
using Nostr.Client.Communicator;
using Nostr.Client.Messages;
using Nostr.Client.Requests;
using Nostr.Client.Responses;

namespace Angor.Contexts.Data.Services;

public class NostrService : INostrService, IDisposable
{
    private readonly ILogger<NostrService> _logger;
    private readonly List<string> _relayUrls;
    private readonly List<INostrClient> _clients;
    private readonly List<INostrCommunicator> _communicators;

    private readonly ILoggerFactory _loggerFactory;
    
    private readonly string[] _defaultRelays = {
        "wss://relay.damus.io",
        "wss://nos.lol", 
        "wss://relay.nostr.band"
    };

    public NostrService(ILoggerProvider loggerProvider)
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
        _relayUrls = new List<string>(_defaultRelays);
        _clients = new List<INostrClient>();
        _communicators = new List<INostrCommunicator>();
    }

    public async Task<List<NostrEventResponse>> GetEventsByKindAsync(int kind, params string[] eventIds)
    {
        return await GetEventsByKindAsync(kind, 10, eventIds);
    }

    public async Task<List<NostrEventResponse>> GetEventsByKindAsync(int kind, int timeoutSeconds = 10, params string[] eventIds)
    {
        var allEvents = new List<NostrEventResponse>();
        var tasks = new List<Task<List<NostrEventResponse>>>();

        _logger.LogInformation("Fetching events of kind {Kind} from {RelayCount} relays with timeout {Timeout}s", 
            kind, _relayUrls.Count, timeoutSeconds);

        // Create tasks for each relay
        foreach (var relayUrl in _relayUrls)
        {
            tasks.Add(GetEventsFromSingleRelayAsync(relayUrl, (NostrKind)kind, timeoutSeconds, eventIds));
        }

        // Wait for all tasks to complete
        var results = await Task.WhenAll(tasks);
        
        // Combine all results
        foreach (var events in results)
        {
            allEvents.AddRange(events);
        }

        // Remove duplicates based on event ID
        var uniqueEvents = allEvents
            .GroupBy(e => e.Event?.Id)
            .Select(g => g.First())
            .Where(e => e.Event != null)
            .ToList();

        _logger.LogInformation("Retrieved {UniqueCount} unique events from {TotalCount} total events", 
            uniqueEvents.Count, allEvents.Count);

        return uniqueEvents;
    }

    private async Task<List<NostrEventResponse>> GetEventsFromSingleRelayAsync(string relayUrl, NostrKind kind, int timeoutSeconds, string[] eventIds)
    {
        var events = new List<NostrEventResponse>();
        var taskCompletionSource = new TaskCompletionSource<List<NostrEventResponse>>();
        
        try
        {
            using var communicator = new NostrWebsocketCommunicator(new Uri(relayUrl));
            using var client = new NostrWebsocketClient(communicator, _loggerFactory.CreateLogger<NostrWebsocketClient>());

            // Setup event handlers
            client.Streams.EventStream.Subscribe(response =>
            {
                if (response?.Event?.Kind == kind)
                {
                    events.Add(response);
                }
            });

            client.Streams.EoseStream.Subscribe(_ => 
            {
                _logger.LogDebug("End of stored events received from {Relay}", relayUrl);
                taskCompletionSource.TrySetResult(events);
            });

            client.Streams.NoticeStream.Subscribe(notice =>
            {
                _logger.LogWarning("Notice from {Relay}: {Message}", relayUrl, notice.Message);
            });

            // Connect to relay
            await communicator.Start();

            // Create filter
            var filter = new NostrFilter
            {
                Kinds = new[] { kind },
                Ids = eventIds?.Length > 0 ? eventIds : null
            };

            // Subscribe to events
            var subscriptionId = $"sub-{kind}-{Guid.NewGuid():N}";
            client.Send(new NostrRequest(subscriptionId, filter));

            // Wait for events or timeout
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            var completedTask = await Task.WhenAny(taskCompletionSource.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _logger.LogWarning("Timeout waiting for events from relay: {Relay}", relayUrl);
                taskCompletionSource.TrySetResult(events);
            }

            _logger.LogDebug("Retrieved {Count} events from relay: {Relay}", events.Count, relayUrl);
            return await taskCompletionSource.Task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to relay: {Relay}", relayUrl);
            return new List<NostrEventResponse>();
        }
    }

    public async Task ConnectToRelaysAsync()
    {
        _logger.LogInformation("Connecting to {Count} relays...", _relayUrls.Count);
        
        await DisconnectFromRelaysAsync(); // Clean up existing connections
        
        foreach (var relayUrl in _relayUrls)
        {
            try
            {
                var communicator = new NostrWebsocketCommunicator(new Uri(relayUrl));
                var client = new NostrWebsocketClient(communicator, _loggerFactory.CreateLogger<NostrWebsocketClient>());
                
                await communicator.Start();
                
                _communicators.Add(communicator);
                _clients.Add(client);
                
                _logger.LogDebug("Connected to relay: {Relay}", relayUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to relay: {Relay}", relayUrl);
            }
        }
        
        _logger.LogInformation("Successfully connected to {Count} relays", _clients.Count);
    }

    public async Task DisconnectFromRelaysAsync()
    {
        _logger.LogInformation("Disconnecting from all relays...");
        
        foreach (var client in _clients)
        {
            try
            {
                client?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing client");
            }
        }
        
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
        
        _clients.Clear();
        _communicators.Clear();
        
        _logger.LogInformation("Disconnected from all relays");
    }

    public void AddRelay(string relayUrl)
    {
        if (!_relayUrls.Contains(relayUrl))
        {
            _relayUrls.Add(relayUrl);
            _logger.LogInformation("Added relay: {Relay}", relayUrl);
        }
    }

    public void RemoveRelay(string relayUrl)
    {
        if (_relayUrls.Remove(relayUrl))
        {
            _logger.LogInformation("Removed relay: {Relay}", relayUrl);
        }
    }

    public List<string> GetConnectedRelays()
    {
        return new List<string>(_relayUrls);
    }

    public void Dispose()
    {
        DisconnectFromRelaysAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}
using Microsoft.Extensions.Logging;
using Nostr.Client.Messages;
using Nostr.Client.Responses;
using System.Reactive.Linq;
using Nostr.Client.Requests;

namespace Angor.Contexts.Data.Services;

public class NostrService : INostrService, IDisposable
{
    private readonly ILogger<NostrService> _logger;
    private readonly INostrClientWrapper _clientWrapper;

    private const int TimeoutSeconds = 10;

    public NostrService(ILogger<NostrService> logger, INostrClientWrapper clientWrapper)
    {
        _logger = logger;
        _clientWrapper = clientWrapper;
    }

    public async Task<List<NostrEventResponse>> GetEventsAsync(string subscriptionId,string[] receiversPubkeys, NostrKind[] kinds, string[]? sendersPubkeys = null,
        string[]? eventIds = null)
    {
        _logger.LogInformation("Fetching events of kind {Kind} with timeout {Timeout}s", kinds, TimeoutSeconds);

        try
        {
            // Ensure we're connected
            if (!_clientWrapper.IsConnected)
            {
                await ConnectToRelaysAsync();
            }

            // Get the observable stream of events
            var eventObservable = _clientWrapper.SubscribeToEvents(subscriptionId ,new NostrFilter { Kinds = kinds, P = receiversPubkeys,
                Authors = sendersPubkeys, Ids = eventIds });

            // Collect events with timeout
            var events = await eventObservable
                .Timeout(TimeSpan.FromSeconds(TimeoutSeconds))
                .Catch(Observable.Return<NostrEventResponse>(null)) // Handle timeout gracefully
                .Where(e => e != null) // Filter out null events from timeout
                .ToList(); // Collect all events into a list

            _logger.LogInformation("Retrieved {Count} events of kinds {Kind} from {RelayCount} relays", 
                events.Count, kinds, _clientWrapper.ConnectedRelaysCount);
                
            return events.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching events of kind {Kind}", kinds);
            return [];
        }
    }
    
    
    public async Task ConnectToRelaysAsync()
    {
        _logger.LogInformation("Connecting to relays...");
        
        try
        {
            await _clientWrapper.ConnectAsync();
            _logger.LogInformation("Successfully connected to {Count} relays", _clientWrapper.ConnectedRelaysCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to relays");
            throw;
        }
    }

    public async Task DisconnectFromRelaysAsync()
    {
        _clientWrapper.Disconnect();
    }

    public void AddRelay(string relayUrl)
    {
        _clientWrapper.AddRelay(relayUrl);
    }

    public void RemoveRelay(string relayUrl)
    {
        _clientWrapper.RemoveRelay(relayUrl);
    }

    public List<string> GetConnectedRelays()
    {
        return _clientWrapper.GetConfiguredRelays();
    }

    public void Dispose()
    {
        ((IDisposable)_clientWrapper)?.Dispose();
        GC.SuppressFinalize(this);
    }
}
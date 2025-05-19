using System.Reactive.Linq;
using Nostr.Client.Client;
using Nostr.Client.Requests;
using Nostr.Client.Responses;

namespace Angor.Contests.CrossCutting;

/// <summary>
/// Client that provides query capabilities for Nostr events through multiple WebSocket connections.
/// Completes each per-relay event stream upon its EOSE and merges them.
/// </summary>
public class NostrQueryClient
{
    private readonly INostrClient[] clients;

    /// <summary>
    /// Initializes a new instance of the <see cref="NostrQueryClient"/> class.
    /// </summary>
    /// <param name="clients">Array of Nostr clients (one per relay) to query against.</param>
    public NostrQueryClient(params INostrClient[] clients)
    {
        if (clients == null || clients.Length == 0)
            throw new ArgumentException("At least one INostrClient must be provided.", nameof(clients));

        this.clients = clients;
    }

    /// <summary>
    /// Queries events matching the filter. Completes when each client has signaled EOSE or the timeout elapses.
    /// </summary>
    /// <param name="filter">Filter to apply to events.</param>
    /// <param name="timeout">Maximum time to wait for events.</param>
    /// <returns>An observable sequence of <see cref="NostrEventResponse"/>.</returns>
    public IObservable<NostrEventResponse> Query(NostrFilter filter, TimeSpan timeout)
    {
        if (filter is null)
            throw new ArgumentNullException(nameof(filter));

        var subscriptionId = Guid.NewGuid().ToString();

        // Send request to each client
        foreach (var client in clients)
        {
            client.Send(new NostrRequest(subscriptionId, filter));
        }

        // Build a per-client event stream that completes on its EOSE
        var perClientStreams = clients.Select(client =>
            client.Streams.EventStream
                .Where(evt => evt.Subscription == subscriptionId)
                .DistinctUntilChanged(x =>x.Event?.Id)
                .TakeUntil(
                    client.Streams.EoseStream
                        .Where(eose => eose.Subscription == subscriptionId)
                )
        );

        // Merge all client streams; completes when all inner streams complete
        var mergedStream = perClientStreams.Merge();

        // Timeout trigger
        var timeoutTrigger = Observable.Timer(timeout);

        // Terminate on timeout or when all streams complete
        return mergedStream
            .TakeUntil(timeoutTrigger)
            .Finally(() =>
            {
                foreach (var client in clients)
                {
                    client.Send(new NostrCloseRequest(subscriptionId));
                }
            });
    }
}
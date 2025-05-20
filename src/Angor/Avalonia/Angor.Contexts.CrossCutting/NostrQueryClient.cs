using System.Reactive.Linq;
using Nostr.Client.Client;
using Nostr.Client.Requests;
using Nostr.Client.Responses;

namespace Angor.Contests.CrossCutting
{
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
        /// Queries events matching the filter. Caller controls timing and disposal via Rx operators.
        /// </summary>
        /// <param name="filter">Filter to apply to events.</param>
        /// <returns>An observable sequence of <see cref="NostrEventResponse"/>.</returns>
        public IObservable<NostrEventResponse> Query(NostrFilter filter)
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
                    .DistinctUntilChanged(x => x.Event?.Id)
                    .TakeUntil(
                        client.Streams.EoseStream
                            .Where(eose => eose.Subscription == subscriptionId)
                    )
            );

            // Merge all client streams and ensure cleanup on completion or disposal
            return perClientStreams
                .Merge()
                .Finally(() =>
                {
                    foreach (var client in clients)
                    {
                        client.Send(new NostrCloseRequest(subscriptionId));
                    }
                });
        }
    }
}
using System;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Nostr.Client.Client;
using Nostr.Client.Communicator;
using Nostr.Client.Requests;

namespace AngorApp.UI.Sections.Founder.CreateProject.Moonshot;

public class MoonshotService : IMoonshotService
{
    private readonly INostrCommunicationFactory _communicationFactory;
    private readonly INetworkService _networkService;
    private readonly ILogger<MoonshotService> _logger;

    public MoonshotService(
        INostrCommunicationFactory communicationFactory,
        INetworkService networkService,
        ILogger<MoonshotService> logger)
    {
        _communicationFactory = communicationFactory;
        _networkService = networkService;
        _logger = logger;
    }

    public async Task<Result<MoonshotProjectData>> GetMoonshotProjectAsync(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return Result.Failure<MoonshotProjectData>("Event ID cannot be empty.");
        }

        // Clean the event ID (remove any whitespace)
        eventId = eventId.Trim();

        try
        {
            var nostrClient = _communicationFactory.GetOrCreateClient(_networkService);

            var tcs = new TaskCompletionSource<Result<MoonshotProjectData>>();
            var subscriptionId = Guid.NewGuid().ToString("N");
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            // Register for timeout
            cts.Token.Register(() =>
                {
                    if (!tcs.Task.IsCompleted)
                    {
                        tcs.TrySetResult(Result.Failure<MoonshotProjectData>("Timeout waiting for Nostr event."));
                    }
                });

            // Subscribe to events
            var subscription = nostrClient.Streams.EventStream
                        .Where(e => e.Subscription == subscriptionId)
                        .Take(1)
                        .Subscribe(
                        response =>
                       {
                           try
                           {
                               if (response.Event?.Content == null)
                               {
                                   tcs.TrySetResult(Result.Failure<MoonshotProjectData>("Event content is empty."));
                                   return;
                               }

                               var content = response.Event.Content;
                               _logger.LogDebug("Received Moonshot event: {Content}", content);

                               var moonshotData = JsonSerializer.Deserialize<MoonshotProjectData>(content);

                               if (moonshotData == null)
                               {
                                   tcs.TrySetResult(Result.Failure<MoonshotProjectData>("Failed to parse Moonshot data."));
                                   return;
                               }

                               tcs.TrySetResult(Result.Success(moonshotData));
                           }
                           catch (JsonException ex)
                           {
                               _logger.LogError(ex, "Failed to deserialize Moonshot event content");
                               tcs.TrySetResult(Result.Failure<MoonshotProjectData>($"Failed to parse event content: {ex.Message}"));
                           }
                       },
                        ex =>
                       {
                           _logger.LogError(ex, "Error receiving Nostr event");
                           tcs.TrySetResult(Result.Failure<MoonshotProjectData>($"Error receiving event: {ex.Message}"));
                       });

            // Subscribe to EOSE to know when no more events will come
            var eoseSubscription = nostrClient.Streams.EoseStream
                .Where(e => e.Subscription == subscriptionId)
                .Take(1)
                .Subscribe(_ =>
                {
                    // Give a small delay after EOSE before failing
                    Task.Delay(500).ContinueWith(_ =>
                    {
                        if (!tcs.Task.IsCompleted)
                        {
                            tcs.TrySetResult(Result.Failure<MoonshotProjectData>($"Event not found with ID: {eventId}"));
                        }
                    });
                });

            // Send the request
            var filter = new NostrFilter
            {
                Ids = new[] { eventId },
                Limit = 1
            };

            nostrClient.Send(new NostrRequest(subscriptionId, filter));

            var result = await tcs.Task;

            // Cleanup
            subscription.Dispose();
            eoseSubscription.Dispose();
            cts.Dispose();

            // Send close request
            nostrClient.Send(new NostrCloseRequest(subscriptionId));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Moonshot project with event ID: {EventId}", eventId);
            return Result.Failure<MoonshotProjectData>($"Failed to fetch Moonshot project: {ex.Message}");
        }
    }
}

using System.Reactive.Linq;
using System.Text.Json;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;
using Nostr.Client.Requests;

namespace Angor.Sdk.Funding.Founder.Operations;

public static class GetMoonshotProject
{
    public sealed record GetMoonshotProjectRequest(string EventId) : IRequest<Result<GetMoonshotProjectResponse>>;

    public sealed record GetMoonshotProjectResponse(MoonshotProjectData MoonshotProjectData);

    internal sealed class GetMoonshotProjectHandler(
        INostrCommunicationFactory communicationFactory,
        INetworkService networkService,
        ILogger<GetMoonshotProjectHandler> logger)
        : IRequestHandler<GetMoonshotProjectRequest, Result<GetMoonshotProjectResponse>>
    {
        public async Task<Result<GetMoonshotProjectResponse>> Handle(GetMoonshotProjectRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.EventId))
            {
                return Result.Failure<GetMoonshotProjectResponse>("Event ID cannot be empty.");
            }

            // Clean the event ID (remove any whitespace)
            var eventId = request.EventId.Trim();

            try
            {
                var nostrClient = communicationFactory.GetOrCreateClient(networkService);

                var tcs = new TaskCompletionSource<Result<GetMoonshotProjectResponse>>();
                var subscriptionId = Guid.NewGuid().ToString("N");
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

                // Register for timeout
                cts.Token.Register(() =>
                {
                    if (!tcs.Task.IsCompleted)
                    {
                        tcs.TrySetResult(Result.Failure<GetMoonshotProjectResponse>("Timeout waiting for Nostr event."));
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
                                    tcs.TrySetResult(Result.Failure<GetMoonshotProjectResponse>("Event content is empty."));
                                    return;
                                }

                                var content = response.Event.Content;
                                logger.LogDebug("Received Moonshot event: {Content}", content);

                                var moonshotData = JsonSerializer.Deserialize<MoonshotProjectData>(content);

                                if (moonshotData == null)
                                {
                                    tcs.TrySetResult(Result.Failure<GetMoonshotProjectResponse>("Failed to parse Moonshot data."));
                                    return;
                                }

                                tcs.TrySetResult(Result.Success(new GetMoonshotProjectResponse(moonshotData)));
                            }
                            catch (JsonException ex)
                            {
                                logger.LogError(ex, "Failed to deserialize Moonshot event content");
                                tcs.TrySetResult(Result.Failure<GetMoonshotProjectResponse>($"Failed to parse event content: {ex.Message}"));
                            }
                        },
                        ex =>
                        {
                            logger.LogError(ex, "Error receiving Nostr event");
                            tcs.TrySetResult(Result.Failure<GetMoonshotProjectResponse>($"Error receiving event: {ex.Message}"));
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
                                tcs.TrySetResult(Result.Failure<GetMoonshotProjectResponse>($"Event not found with ID: {eventId}"));
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
                logger.LogError(ex, "Failed to fetch Moonshot project with event ID: {EventId}", eventId);
                return Result.Failure<GetMoonshotProjectResponse>($"Failed to fetch Moonshot project: {ex.Message}");
            }
        }
    }
}


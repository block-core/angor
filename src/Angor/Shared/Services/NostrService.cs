using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using Nostr.Client.Client;
using Nostr.Client.Messages;
using Nostr.Client.Requests;
using Nostr.Client.Responses;

namespace Angor.Shared.Services;

public class NostrService(INostrCommunicationFactory nostrClient, INetworkService networkService) : INostrService
{
    private readonly INostrClient nostrClient = nostrClient.GetOrCreateClient(networkService);

    public async Task<Result<NostrOkResponse>> Send(NostrEvent nostrEvent)
    {
        try
        {
            var okStream = nostrClient
                .Streams.OkStream
                .Where(x => x.EventId == nostrEvent.Id)
                .Replay(1);

            using var connection = okStream.Connect();

            nostrClient.Send(new NostrEventRequest(nostrEvent));

            var firstOkResponse = await okStream
                .Where(x => x.Accepted)
                .FirstAsync()
                .Timeout(TimeSpan.FromSeconds(10));

            return Result.Success(firstOkResponse);
        }
        catch (Exception ex)
        {
            return Result.Failure<NostrOkResponse>(ex.Message);
        }
    }
}
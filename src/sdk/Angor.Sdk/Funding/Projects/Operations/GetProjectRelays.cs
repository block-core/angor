using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Sdk.Funding.Projects.Operations;

public static class GetProjectRelays
{
    public record GetProjectRelaysRequest(string NostrPubKey) : IRequest<Result<GetProjectRelaysResponse>>;

    public record GetProjectRelaysResponse(IEnumerable<string> RelayUrls);

    public class GetProjectRelaysHandler(IRelayService relayService)
        : IRequestHandler<GetProjectRelaysRequest, Result<GetProjectRelaysResponse>>
    {
        public async Task<Result<GetProjectRelaysResponse>> Handle(GetProjectRelaysRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(request.NostrPubKey))
                return Result.Success(new GetProjectRelaysResponse(Enumerable.Empty<string>()));

            var tcs = new TaskCompletionSource<List<string>>();
            var results = new List<string>();

            relayService.LookupRelayListForNPubs(
                (pubkey, relayTags) =>
                {
                    var relayUrls = relayTags
                        .Select(tag => tag.AdditionalData.FirstOrDefault())
                        .Where(url => !string.IsNullOrEmpty(url))
                        .ToList();

                    results.AddRange(relayUrls!);
                },
                () => tcs.TrySetResult(results),
                request.NostrPubKey);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            cts.Token.Register(() => tcs.TrySetResult(results));

            var completedResults = await tcs.Task;
            return Result.Success(new GetProjectRelaysResponse(completedResults.AsEnumerable()));
        }
    }
}

using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Sdk.Funding.Projects.Operations;

/// <summary>
/// Kick a background refresh of a project's cached profile metadata (name, banner,
/// picture, about) from Nostr relays. Intended to be fired whenever a user lands on
/// a project page (Find Projects, Portfolio, My Projects) so founder profile updates
/// propagate to every user — not just the founder who published them.
/// The refresh is skipped when the cached metadata is younger than the freshness TTL
/// unless <c>Force</c> is set (used by the founder right after publishing an update).
/// </summary>
public static class RevalidateProjectMetadata
{
    public record RevalidateProjectMetadataRequest(ProjectId ProjectId, bool Force = false)
        : IRequest<Result<RevalidateProjectMetadataResponse>>;

    public record RevalidateProjectMetadataResponse;

    public class RevalidateProjectMetadataHandler(IProjectService projectService)
        : IRequestHandler<RevalidateProjectMetadataRequest, Result<RevalidateProjectMetadataResponse>>
    {
        public Task<Result<RevalidateProjectMetadataResponse>> Handle(
            RevalidateProjectMetadataRequest request, CancellationToken cancellationToken)
        {
            return projectService
                .RevalidateAsync(request.ProjectId, request.Force)
                .Map(() => new RevalidateProjectMetadataResponse());
        }
    }
}

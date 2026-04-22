using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Angor.Sdk.Funding.Projects.Operations;

/// <summary>
/// Shared data model for a FAQ item used in project profile editing.
/// Matches the angor-profile web app's FAQ structure.
/// </summary>
public record FaqItem
{
    [JsonProperty("question")] public string? Question { get; set; }
    [JsonProperty("answer")] public string? Answer { get; set; }
}

/// <summary>
/// Shared data model for a media item used in project profile editing.
/// </summary>
public record MediaItem
{
    [JsonProperty("url")] public string? Url { get; set; }
    [JsonProperty("type")] public string? Type { get; set; }
}

public static class FetchProjectProfileData
{
    public record FetchProjectProfileDataRequest(ProjectId ProjectId) : IRequest<Result<FetchProjectProfileDataResponse>>;

    public record FetchProjectProfileDataResponse(
        ProjectMetadata? Metadata,
        string? ProjectContent,
        IReadOnlyList<FaqItem>? FaqItems,
        IReadOnlyList<string>? MemberPubkeys,
        IReadOnlyList<MediaItem>? MediaItems);

    public class FetchProjectProfileDataHandler(
        IProjectService projectService,
        IRelayService relayService,
        ILogger<FetchProjectProfileDataHandler> logger)
        : IRequestHandler<FetchProjectProfileDataRequest, Result<FetchProjectProfileDataResponse>>
    {
        public async Task<Result<FetchProjectProfileDataResponse>> Handle(
            FetchProjectProfileDataRequest request,
            CancellationToken cancellationToken)
        {
            // Resolve the project's Nostr public key
            var projectResult = await projectService.GetAsync(request.ProjectId);
            if (projectResult.IsFailure)
                return Result.Failure<FetchProjectProfileDataResponse>($"Project not found: {projectResult.Error}");

            var nostrPubKeyHex = projectResult.Value.NostrPubKey;
            if (string.IsNullOrEmpty(nostrPubKeyHex))
                return Result.Success(new FetchProjectProfileDataResponse(null, null, null, null, null));

            // Fetch all data in parallel
            var metadataTask = relayService.FetchProfileMetadataAsync(nostrPubKeyHex);
            var projectContentTask = relayService.FetchAppSpecificDataAsync(nostrPubKeyHex, "angor:project");
            var faqTask = relayService.FetchAppSpecificDataAsync(nostrPubKeyHex, "angor:faq");
            var membersTask = relayService.FetchAppSpecificDataAsync(nostrPubKeyHex, "angor:members");
            var mediaTask = relayService.FetchAppSpecificDataAsync(nostrPubKeyHex, "angor:media");

            await Task.WhenAll(metadataTask, projectContentTask, faqTask, membersTask, mediaTask);

            var metadata = await metadataTask;
            var projectContent = await projectContentTask;
            var faqJson = await faqTask;
            var membersJson = await membersTask;
            var mediaJson = await mediaTask;

            List<FaqItem>? faqItems = null;
            if (!string.IsNullOrEmpty(faqJson))
            {
                try { faqItems = JsonConvert.DeserializeObject<List<FaqItem>>(faqJson); }
                catch (Exception ex) { logger.LogWarning(ex, "Failed to parse FAQ JSON for {ProjectId}", request.ProjectId.Value); }
            }

            List<string>? members = null;
            if (!string.IsNullOrEmpty(membersJson))
            {
                try
                {
                    var obj = JsonConvert.DeserializeObject<MembersJson>(membersJson);
                    members = obj?.Pubkeys;
                }
                catch (Exception ex) { logger.LogWarning(ex, "Failed to parse members JSON for {ProjectId}", request.ProjectId.Value); }
            }

            List<MediaItem>? mediaItems = null;
            if (!string.IsNullOrEmpty(mediaJson))
            {
                try { mediaItems = JsonConvert.DeserializeObject<List<MediaItem>>(mediaJson); }
                catch (Exception ex) { logger.LogWarning(ex, "Failed to parse media JSON for {ProjectId}", request.ProjectId.Value); }
            }

            return Result.Success(new FetchProjectProfileDataResponse(
                metadata,
                projectContent,
                faqItems,
                members,
                mediaItems));
        }

        private record MembersJson
        {
            [JsonProperty("pubkeys")] public List<string>? Pubkeys { get; set; }
        }
    }
}

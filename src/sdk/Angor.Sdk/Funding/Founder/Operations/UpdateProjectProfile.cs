using Angor.Sdk.Common;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Projects.Operations;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Nostr.Client.Messages.Metadata;
using Nostr.Client.Responses;

namespace Angor.Sdk.Funding.Founder.Operations;

public static class UpdateProjectProfile
{
    public record UpdateProjectProfileRequest(
        WalletId WalletId,
        ProjectId ProjectId,
        ProjectMetadata Metadata,
        string? ProjectContent,
        IReadOnlyList<FaqItem>? FaqItems,
        IReadOnlyList<string>? MemberPubkeys,
        IReadOnlyList<MediaItem>? MediaItems) : IRequest<Result<UpdateProjectProfileResponse>>;

    public record UpdateProjectProfileResponse(string EventId);

    public class UpdateProjectProfileHandler(
        ISeedwordsProvider seedwordsProvider,
        IDerivationOperations derivationOperations,
        IProjectService projectService,
        IRelayService relayService,
        ILogger<UpdateProjectProfileHandler> logger)
        : IRequestHandler<UpdateProjectProfileRequest, Result<UpdateProjectProfileResponse>>
    {
        public async Task<Result<UpdateProjectProfileResponse>> Handle(
            UpdateProjectProfileRequest request,
            CancellationToken cancellationToken)
        {
            // Resolve project to get FounderKey and NostrPubKey
            var projectResult = await projectService.GetAsync(request.ProjectId);
            if (projectResult.IsFailure)
                return Result.Failure<UpdateProjectProfileResponse>($"Project not found: {projectResult.Error}");

            var founderKey = projectResult.Value.FounderKey;
            if (string.IsNullOrEmpty(founderKey))
                return Result.Failure<UpdateProjectProfileResponse>("Project founder key is not set.");

            // Derive the project's Nostr private key from the wallet seed
            var wallet = await seedwordsProvider.GetSensitiveData(request.WalletId.Value);
            if (wallet.IsFailure)
                return Result.Failure<UpdateProjectProfileResponse>(wallet.Error);

            var nostrPrivateKey = await derivationOperations.DeriveProjectNostrPrivateKeyAsync(
                wallet.Value.ToWalletWords(),
                founderKey);

            var nostrKeyHex = Encoders.Hex.EncodeData(nostrPrivateKey.ToBytes());

            // Publish kind 0 (profile metadata)
            var profileTcs = new TaskCompletionSource<Result<string>>();
            var nostrMetadata = new NostrMetadata
            {
                Name = request.Metadata.Name,
                DisplayName = request.Metadata.DisplayName,
                About = request.Metadata.About,
                Picture = request.Metadata.Picture,
                Banner = request.Metadata.Banner,
                Nip05 = request.Metadata.Nip05,
                Lud16 = request.Metadata.Lud16,
                Website = request.Metadata.Website,
            };

            await relayService.CreateNostrProfileAsync(nostrMetadata, nostrKeyHex, ok =>
            {
                if (!ok.Accepted)
                {
                    logger.LogDebug("Failed to update Nostr profile for project {ProjectId}: {CommunicatorName} - {Message}",
                        request.ProjectId.Value, ok.CommunicatorName, ok.Message);
                    profileTcs.TrySetResult(Result.Failure<string>($"Relay rejected profile update: {ok.Message}"));
                    return;
                }

                profileTcs.TrySetResult(Result.Success(ok.EventId ?? ""));
            });

            var profileResult = await profileTcs.Task;
            if (profileResult.IsFailure)
                return Result.Failure<UpdateProjectProfileResponse>(profileResult.Error);

            // Publish app-specific data events (kind 30078)
            if (request.ProjectContent != null)
                await relayService.PublishAppSpecificDataAsync("angor:project", request.ProjectContent, nostrKeyHex, NoopCallback);

            if (request.FaqItems != null)
                await relayService.PublishAppSpecificDataAsync("angor:faq", JsonConvert.SerializeObject(request.FaqItems), nostrKeyHex, NoopCallback);

            if (request.MemberPubkeys != null)
                await relayService.PublishAppSpecificDataAsync("angor:members", JsonConvert.SerializeObject(new { pubkeys = request.MemberPubkeys }), nostrKeyHex, NoopCallback);

            if (request.MediaItems != null)
                await relayService.PublishAppSpecificDataAsync("angor:media", JsonConvert.SerializeObject(request.MediaItems), nostrKeyHex, NoopCallback);

            return Result.Success(new UpdateProjectProfileResponse(profileResult.Value));
        }

        private void NoopCallback(NostrOkResponse ok)
        {
            if (!ok.Accepted)
                logger.LogDebug("Failed to publish app-specific data: {CommunicatorName} - {Message}",
                    ok.CommunicatorName, ok.Message);
        }
    }
}

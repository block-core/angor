using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;
using Nostr.Client.Messages.Metadata;

namespace Angor.Sdk.Funding.Founder.Operations;

public static class UpdateProjectProfile
{
    public record UpdateProjectProfileRequest(
        WalletId WalletId,
        ProjectSeedDto ProjectSeedDto,
        ProjectMetadata Metadata,
        string? ProjectContent,
        string? FaqContent,
        string? MembersContent,
        string? MediaContent) : IRequest<Result<UpdateProjectProfileResponse>>;

    public record UpdateProjectProfileResponse(string EventId);

    public class UpdateProjectProfileHandler(
        ISeedwordsProvider seedwordsProvider,
        IDerivationOperations derivationOperations,
        IRelayService relayService,
        ILogger<UpdateProjectProfileHandler> logger)
        : IRequestHandler<UpdateProjectProfileRequest, Result<UpdateProjectProfileResponse>>
    {
        public async Task<Result<UpdateProjectProfileResponse>> Handle(
            UpdateProjectProfileRequest request,
            CancellationToken cancellationToken)
        {
            var wallet = await seedwordsProvider.GetSensitiveData(request.WalletId.Value);
            if (wallet.IsFailure)
                return Result.Failure<UpdateProjectProfileResponse>(wallet.Error);

            var nostrPrivateKey = await derivationOperations.DeriveProjectNostrPrivateKeyAsync(
                wallet.Value.ToWalletWords(),
                request.ProjectSeedDto.FounderKey);

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
                    logger.LogDebug("Failed to update Nostr profile: {CommunicatorName} - {Message}",
                        ok.CommunicatorName, ok.Message);
                    profileTcs.TrySetResult(Result.Failure<string>($"Relay rejected profile update: {ok.Message}"));
                    return;
                }

                profileTcs.TrySetResult(Result.Success(ok.EventId ?? ""));
            });

            var profileResult = await profileTcs.Task;
            if (profileResult.IsFailure)
                return Result.Failure<UpdateProjectProfileResponse>(profileResult.Error);

            // Publish app-specific data events (kind 30078) for project content sections
            await PublishIfNotNull("angor:project", request.ProjectContent, nostrKeyHex);
            await PublishIfNotNull("angor:faq", request.FaqContent, nostrKeyHex);
            await PublishIfNotNull("angor:members", request.MembersContent, nostrKeyHex);
            await PublishIfNotNull("angor:media", request.MediaContent, nostrKeyHex);

            return Result.Success(new UpdateProjectProfileResponse(profileResult.Value));
        }

        private Task PublishIfNotNull(string dTag, string? content, string nostrKeyHex)
        {
            if (content == null)
                return Task.CompletedTask;

            return relayService.PublishAppSpecificDataAsync(dTag, content, nostrKeyHex, ok =>
            {
                if (!ok.Accepted)
                {
                    logger.LogDebug("Failed to publish {DTag}: {CommunicatorName} - {Message}",
                        dTag, ok.CommunicatorName, ok.Message);
                }
            });
        }
    }
}

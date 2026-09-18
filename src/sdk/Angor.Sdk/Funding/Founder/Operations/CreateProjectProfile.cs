using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Shared;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using NBitcoin;
using NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;
using Nostr.Client.Messages.Metadata;
using static Angor.Sdk.Funding.Founder.Operations.CreateProjectInfo;
using Angor.Sdk.Funding.Projects.Dtos;

namespace Angor.Sdk.Funding.Founder.Operations;

public static class CreateProjectProfile
{
    public record CreateProjectProfileRequest(
        WalletId WalletId,
        ProjectSeedDto ProjectSeedDto,
        CreateProjectDto Project) : IRequest<Result<CreateProjectProfileResponse>>; // Returns Nostr event ID

    public record CreateProjectProfileResponse(
        string EventId);

    public class CreateProjectProfileHandler(
                ISeedwordsProvider seedwordsProvider,
                IDerivationOperations derivationOperations,
                IAngorIndexerService angorIndexerService,
                IRelayService relayService,
                IGenericDocumentCollection<DerivedProjectKeys> derivedProjectKeysCollection,
                ILogger<CreateProjectProfileHandler> logger,
                TimeSpan? nip65AckTimeout = null) 
        : IRequestHandler<CreateProjectProfileRequest, Result<CreateProjectProfileResponse>>
    {
        /// <summary>
        /// How long to wait for the NIP-65 relay list to be acknowledged before continuing.
        /// NIP-65 is published to the discovery relays, which are a different (and smaller) set
        /// than the relays that store the profile itself. If every discovery relay is unreachable
        /// no OK ever arrives, so this wait is bounded to keep an outage there from blocking deployment.
        /// </summary>
        private static readonly TimeSpan DefaultNip65AckTimeout = TimeSpan.FromSeconds(10);

        private TimeSpan Nip65AckTimeout { get; } = nip65AckTimeout ?? DefaultNip65AckTimeout;

        public async Task<Result<CreateProjectProfileResponse>> Handle(CreateProjectProfileRequest request, CancellationToken cancellationToken)
        {
            var wallet = await seedwordsProvider.GetSensitiveData(request.WalletId.Value);

            if (wallet.IsFailure)
            {
                logger.LogDebug("Failed to get sensitive data for WalletId {WalletId}: {Error}", request.WalletId, wallet.Error);
                return Result.Failure<CreateProjectProfileResponse>(wallet.Error);
            }

            ProjectSeedDto newProjectKeys = request.ProjectSeedDto;

            var nostrPrivateKey = await derivationOperations.DeriveProjectNostrPrivateKeyAsync(wallet.Value.ToWalletWords(), newProjectKeys.FounderKey);
            var nostrKeyHex = Encoders.Hex.EncodeData(nostrPrivateKey.ToBytes());

            var profileCreateResult = await CreateNostrProfileAsync(nostrKeyHex, request.Project);

            if (profileCreateResult.IsFailure)
            {
                logger.LogDebug("Failed to create Nostr profile for Project {ProjectName} (WalletId: {WalletId}): {Error}",
                 request.Project.ProjectName, request.WalletId, profileCreateResult.Error);
                return Result.Failure<CreateProjectProfileResponse>(profileCreateResult.Error);
            }

            return Result.Success(new CreateProjectProfileResponse(profileCreateResult.Value));
        }

        private async Task<Result<string>> CreateNostrProfileAsync(string nostrKey, CreateProjectDto project)
        {
            var tcs = new TaskCompletionSource<Result<string>>();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            cts.Token.Register(() => { tcs.TrySetResult(Result.Failure<string>("Nostr profile creation timed out after 30s")); cts.Dispose(); });

            var nostrMetadata = new NostrMetadata
            {
                Name = project.ProjectName,
                Website = project.WebsiteUri,
                About = project.Description,
                Picture = project.AvatarUri,
                Banner = project.BannerUri,
                Nip05 = project.Nip05,
                Lud16 = project.Lud16,
                Nip57 = project.Nip57
            };

            var nip65Published = 0;
            CancellationTokenSource? nip65AckCts = null;

            try
            {
                var resultId = await relayService.CreateNostrProfileAsync(
                    nostrMetadata,
                    nostrKey,
                    okResponse =>
                      {
                          if (!okResponse.Accepted)
                          {
                              logger.LogDebug("Failed to store the project profile on relay for Project {ProjectName}: Communicator {CommunicatorName} - {Message}", project.ProjectName, okResponse.CommunicatorName, okResponse.Message);
                              tcs.TrySetResult(Result.Failure<string>($"Failed to store the project profile on the relay: {okResponse.CommunicatorName} - {okResponse.Message}"));
                              return;
                          }

                          // The OK callback fires once per relay. Only publish NIP-65 once.
                          if (Interlocked.CompareExchange(ref nip65Published, 1, 0) != 0)
                              return;

                          // The profile is already stored at this point. NIP-65 is auxiliary discovery
                          // metadata, so a discovery relay that never answers must not stall the deploy.
                          nip65AckCts = new CancellationTokenSource(Nip65AckTimeout);
                          nip65AckCts.Token.Register(() =>
                          {
                              if (tcs.Task.IsCompleted)
                                  return;

                              logger.LogWarning(
                                  "NIP-65 list was not acknowledged within {Timeout}s for Project {ProjectName}; continuing because the project profile was stored successfully.",
                                  Nip65AckTimeout.TotalSeconds, project.ProjectName);

                              tcs.TrySetResult(Result.Success(okResponse.EventId!));
                          });

                          relayService.PublishNip65List(nostrKey, nip65OkResponse =>
                          {
                             if (tcs.Task.IsCompleted)
                                 return;

                             if (!nip65OkResponse.Accepted)
                                 logger.LogDebug("Failed to publish NIP-65 list for Project {ProjectName}", project.ProjectName);

                             tcs.TrySetResult(!nip65OkResponse.Accepted ?
                                        Result.Failure<string>("Failed to publish NIP-65 list")
                                      : Result.Success(okResponse.EventId!));
                          });
                  });

                return await tcs.Task;
            }
            finally
            {
                nip65AckCts?.Dispose();
            }
        }
    }
}

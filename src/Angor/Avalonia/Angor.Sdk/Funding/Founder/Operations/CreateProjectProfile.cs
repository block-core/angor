using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Shared;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;
using Nostr.Client.Messages.Metadata;
using static Angor.Sdk.Funding.Founder.Operations.CreateProjectInfo;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Shared.Services.Indexer;

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
                ILogger<CreateProjectProfileHandler> logger) 
        : IRequestHandler<CreateProjectProfileRequest, Result<CreateProjectProfileResponse>>
    {
        public async Task<Result<CreateProjectProfileResponse>> Handle(CreateProjectProfileRequest request, CancellationToken cancellationToken)
        {
            var wallet = await seedwordsProvider.GetSensitiveData(request.WalletId.Value);

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

            var resultId = await relayService.CreateNostrProfileAsync(
                nostrMetadata,
                nostrKey,
                okResponse =>
                  {
                      if (!okResponse.Accepted)
                      {
                          logger.LogDebug("Failed to store the project profile on relay for Project {ProjectName}: Communicator {CommunicatorName} - {Message}", project.ProjectName, okResponse.CommunicatorName, okResponse.Message);
                          tcs.SetResult(Result.Failure<string>($"Failed to store the project profile on the relay: {okResponse.CommunicatorName} - {okResponse.Message}"));
                          return;
                      }

                      relayService.PublishNip65List(nostrKey, nip65OkResponse =>
                      {
                         if (tcs.Task.IsCompleted)
                             return;

                         if (!nip65OkResponse.Accepted)
                             logger.LogDebug("Failed to publish NIP-65 list for Project {ProjectName}", project.ProjectName);

                         tcs.SetResult(!nip65OkResponse.Accepted ?
                                    Result.Failure<string>("Failed to publish NIP-65 list")
                                  : Result.Success(okResponse.EventId!));
                      });
              });

            return await tcs.Task;
        }
    }
}

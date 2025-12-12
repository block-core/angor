using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Projects.Application.Dtos;
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
using Stage = Angor.Shared.Models.Stage;

namespace Angor.Sdk.Funding.Founder.Operations;

public static class CreateProjectInfo
{
    public record CreateProjectInfoRequest(
        WalletId WalletId, 
        CreateProjectDto Project,
        ProjectSeedDto ProjectSeedDto) : IRequest<Result<CreateProjectInfoResponse>>; // Returns project info event ID

    public record CreateProjectInfoResponse(
        string EventId);

    public class CreateProjectInfoHandler(
            ISeedwordsProvider seedwordsProvider,
            IDerivationOperations derivationOperations,
            IRelayService relayService,
            IAngorIndexerService angorIndexerService,
            IGenericDocumentCollection<DerivedProjectKeys> derivedProjectKeysCollection,
            ILogger<CreateProjectInfoHandler> logger
            ) : IRequestHandler<CreateProjectInfoRequest, Result<CreateProjectInfoResponse>>
    {
        public async Task<Result<CreateProjectInfoResponse>> Handle(CreateProjectInfoRequest request, CancellationToken cancellationToken)
        {
            if (request.ProjectSeedDto == null)
            {
                logger.LogDebug("FounderKeys is null in CreateProjectRequest for WalletId {WalletId}.", request.WalletId);
                return Result.Failure<CreateProjectInfoResponse>("FounderKeys cannot be null.");
            }

            var wallet = await seedwordsProvider.GetSensitiveData(request.WalletId.Value);

            ProjectSeedDto? newProjectKeys = request.ProjectSeedDto;
          
            var nostrPrivateKey = await derivationOperations.DeriveProjectNostrPrivateKeyAsync(wallet.Value.ToWalletWords(), newProjectKeys.FounderKey);
            var nostrKeyHex = Encoders.Hex.EncodeData(nostrPrivateKey.ToBytes());

            var projectInfoResult = await CreateProjectInfoOnNostr(nostrKeyHex, request.Project, newProjectKeys);

            if (projectInfoResult.IsFailure)
            {
                logger.LogDebug("Failed to create project info on Nostr for Project {ProjectName} (WalletId: {WalletId}): {Error}", request.Project.ProjectName, request.WalletId, projectInfoResult.Error);
                return Result.Failure<CreateProjectInfoResponse>(projectInfoResult.Error);
            }

            return Result.Success(new CreateProjectInfoResponse(projectInfoResult.Value));
        }

        private async Task<Result<string>> CreateProjectInfoOnNostr(string nostrKeyHex, CreateProjectDto project, ProjectSeedDto founderKeys)
        {
            var tsc = new TaskCompletionSource<Result<string>>();

            // Create base ProjectInfo with common properties
            var projectInfo = new ProjectInfo
            {
                ProjectType = project.ProjectType,
                FounderKey = founderKeys.FounderKey,
                FounderRecoveryKey = founderKeys.FounderRecoveryKey,
                NostrPubKey = founderKeys.NostrPubKey,
                ProjectIdentifier = founderKeys.ProjectIdentifier,
                StartDate = project.StartDate
            };

            // Set type-specific properties
            switch (project.ProjectType)
            {
                case ProjectType.Invest:
                    {
                        projectInfo.EndDate = project.EndDate ?? throw new InvalidOperationException("End date is required for Invest projects");
                        projectInfo.ExpiryDate = project.ExpiryDate ?? project.Stages.OrderByDescending(x => x.startDate).First().startDate.AddMonths(2).ToDateTime(TimeOnly.MinValue);
                        projectInfo.PenaltyDays = project.PenaltyDays;
                        projectInfo.PenaltyThreshold = project.PenaltyThreshold;
                        projectInfo.TargetAmount = project.TargetAmount.Sats;
                        projectInfo.Stages = project.Stages.Select(stage => new Stage
                        {
                            AmountToRelease = stage.PercentageOfTotal,
                            ReleaseDate = stage.startDate.ToDateTime(TimeOnly.MinValue),
                        }).ToList();
                        break;
                    }

                case ProjectType.Fund:
                    {
                        projectInfo.ExpiryDate = project.StartDate.AddMonths(6);
                        projectInfo.PenaltyDays = project.PenaltyDays;
                        projectInfo.PenaltyThreshold = project.PenaltyThreshold;
                        projectInfo.DynamicStagePatterns = project.SelectedPatterns ?? throw new InvalidOperationException("Selected patterns are required for Fund projects");
                        projectInfo.Stages = new List<Stage>();
                        break;
                    }

                case ProjectType.Subscribe:
                    {
                        projectInfo.ExpiryDate = project.StartDate.AddMonths(6);
                        projectInfo.DynamicStagePatterns = project.SelectedPatterns ?? throw new InvalidOperationException("Selected patterns are required for Subscribe projects");
                        projectInfo.Stages = new List<Stage>();
                        break;
                    }

                default:
                    tsc.SetResult(Result.Failure<string>("Unsupported project type"));
                    return await tsc.Task;
            }

            var resultId = await relayService.AddProjectAsync(projectInfo, nostrKeyHex,
                   okResponse =>
                   {
                       if (!okResponse.Accepted)
                       {
                           logger.LogDebug("Failed to store project info on relay for Project {ProjectName}: Communicator {CommunicatorName} - {Message}",project.ProjectName, okResponse.CommunicatorName, okResponse.Message);
                           tsc.SetResult(Result.Failure<string>($"Failed to store project info on the relay: {okResponse.CommunicatorName} - {okResponse.Message}"));
                           return;
                       }

                       tsc.SetResult(Result.Success(okResponse.EventId!));
                   });

            return await tsc.Task;
        }
    }
}

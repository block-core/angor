using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;
using Nostr.Client.Messages.Metadata;
using Stage = Angor.Shared.Models.Stage;

namespace Angor.Contexts.Funding.Founder.Operations;

internal static class CreateProjectConstants
{
    public static class CreateProject
    {
        // Updated: added ProjectSeed to request
        public sealed record CreateProjectRequest(
            WalletId WalletId,
            long SelectedFeeRate,
            CreateProjectDto Project,
            ProjectSeedDto ProjectSeed
        ) : IRequest<Result<TransactionDraft>>;

        public sealed class CreateProjectHandler(
            ISeedwordsProvider seedwordsProvider,
            IDerivationOperations derivationOperations,
            IRelayService relayService,
            IFounderTransactionActions founderTransactionActions,
            IWalletOperations walletOperations,
            IAngorIndexerService angorIndexerService,
            INetworkConfiguration networkConfiguration,
            IWalletAccountBalanceService walletAccountBalanceService,
            ILogger<CreateProjectHandler> logger
        ) : IRequestHandler<CreateProjectRequest, Result<TransactionDraft>>
        {
            public async Task<Result<TransactionDraft>> Handle(CreateProjectRequest request, CancellationToken cancellationToken)
            {
                // Validate seed uniqueness
                var existing = await angorIndexerService.GetProjectByIdAsync(request.ProjectSeed.ProjectIdentifier);
                if (existing is not null)
                {
                    logger.LogDebug("Project identifier already used: {ProjectIdentifier} (WalletId: {WalletId})",
                        request.ProjectSeed.ProjectIdentifier, request.WalletId);
                    return Result.Failure<TransactionDraft>("Project identifier already in use.");
                }

                var wallet = await seedwordsProvider.GetSensitiveData(request.WalletId.Value);

                var nostrPrivateKey = await derivationOperations.DeriveProjectNostrPrivateKeyAsync(
                    wallet.Value.ToWalletWords(),
                    request.ProjectSeed.FounderKey);

                var nostrKeyHex = Encoders.Hex.EncodeData(nostrPrivateKey.ToBytes());

                var profileCreateEvent = await CreatNostrProfileAsync(nostrKeyHex, request.Project);
                if (profileCreateEvent.IsFailure)
                {
                    logger.LogDebug("Failed to create Nostr profile for Project {ProjectName} (WalletId: {WalletId}): {Error}",
                        request.Project.ProjectName, request.WalletId, profileCreateEvent.Error);
                    return Result.Failure<TransactionDraft>(profileCreateEvent.Error);
                }

                var founderKeys = new FounderKeys
                {
                    FounderKey = request.ProjectSeed.FounderKey,
                    FounderRecoveryKey = request.ProjectSeed.FounderRecoveryKey,
                    NostrPubKey = request.ProjectSeed.NostrPubKey,
                    ProjectIdentifier = request.ProjectSeed.ProjectIdentifier
                };

                var projectInfo = await CreatProjectInfoOnNostr(nostrKeyHex, request.Project, founderKeys);
                if (projectInfo.IsFailure)
                {
                    logger.LogDebug("Failed to create project info on Nostr for Project {ProjectName} (WalletId: {WalletId}): {Error}",
                        request.Project.ProjectName, request.WalletId, projectInfo.Error);
                    return Result.Failure<TransactionDraft>(projectInfo.Error);
                }

                var transactionInfo = await CreatProjectTransaction(
                    request.WalletId.Value,
                    wallet.Value.ToWalletWords(),
                    request.SelectedFeeRate,
                    founderKeys.FounderKey,
                    founderKeys.ProjectIdentifier,
                    projectInfo.Value);

                if (transactionInfo.IsFailure)
                {
                    logger.LogDebug("Failed to create project transaction for Project {ProjectName} (WalletId: {WalletId}): {Error}",
                        request.Project.ProjectName, request.WalletId.Value, transactionInfo.Error);
                    return Result.Failure<TransactionDraft>(transactionInfo.Error);
                }

                var publishResponse = await walletOperations.PublishTransactionAsync(
                    networkConfiguration.GetNetwork(),
                    transactionInfo.Value.Transaction);

                if (!publishResponse.Success)
                {
                    logger.LogDebug("Failed to publish transaction for Project {ProjectName} (WalletId: {WalletId}): {Message}",
                        request.Project.ProjectName, request.WalletId, publishResponse.Message);
                    return Result.Failure<TransactionDraft>(publishResponse.Message);
                }

                var transactionId = transactionInfo.Value.Transaction.GetHash().ToString();

                return Result.Success(new TransactionDraft
                {
                    SignedTxHex = transactionInfo.Value.Transaction.ToHex(),
                    TransactionId = transactionId,
                    TransactionFee = new Amount(transactionInfo.Value.TransactionFee)
                });
            }

            private async Task<Result<string>> CreatNostrProfileAsync(string nostrKey, CreateProjectDto project)
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

                _ = await relayService.CreateNostrProfileAsync(
                    nostrMetadata,
                    nostrKey,
                    okResponse =>
                    {
                        if (!okResponse.Accepted)
                        {
                            logger.LogDebug("Failed to store project info on relay for {ProjectName}: {Message}",
                                project.ProjectName, okResponse.Message);
                            tcs.SetResult(Result.Failure<string>(
                                $"Failed to store project info on relay: {okResponse.CommunicatorName} - {okResponse.Message}"));
                            return;
                        }

                        relayService.PublishNip65List(nostrKey, nip65OkResponse =>
                        {
                            if (tcs.Task.IsCompleted) return;
                            if (!nip65OkResponse.Accepted)
                                logger.LogDebug("Failed to publish NIP-65 list for Project {ProjectName}", project.ProjectName);

                            tcs.SetResult(!nip65OkResponse.Accepted
                                ? Result.Failure<string>("Failed to publish NIP-65 list")
                                : Result.Success(okResponse.EventId!));
                        });
                    });

                return await tcs.Task;
            }

            private async Task<Result<string>> CreatProjectInfoOnNostr(string nostrKeyHex, CreateProjectDto project, FounderKeys founderKeys)
            {
                var tcs = new TaskCompletionSource<Result<string>>();

                var projectInfo = new ProjectInfo
                {
                    FounderKey = founderKeys.FounderKey,
                    EndDate = project.EndDate ?? throw new InvalidOperationException("End date is required"),
                    StartDate = project.StartDate,
                    ExpiryDate = project.ExpiryDate ?? project.Stages
                        .OrderByDescending(x => x.startDate)
                        .First()
                        .startDate.AddMonths(2)
                        .ToDateTime(TimeOnly.MinValue),
                    FounderRecoveryKey = founderKeys.FounderRecoveryKey,
                    NostrPubKey = founderKeys.NostrPubKey,
                    PenaltyDays = project.PenaltyDays,
                    PenaltyThreshold = project.PenaltyThreshold,
                    ProjectIdentifier = founderKeys.ProjectIdentifier,
                    TargetAmount = project.TargetAmount.Sats,
                    Stages = project.Stages.Select(stage => new Stage
                    {
                        AmountToRelease = stage.PercentageOfTotal,
                        ReleaseDate = stage.startDate.ToDateTime(TimeOnly.MinValue),
                    }).ToList()
                };

                _ = await relayService.AddProjectAsync(projectInfo, nostrKeyHex,
                    okResponse =>
                    {
                        if (!okResponse.Accepted)
                        {
                            logger.LogDebug("Failed to store project info on relay for {ProjectName}: {Message}",
                                project.ProjectName, okResponse.Message);
                            tcs.SetResult(Result.Failure<string>(
                                $"Failed to store project info on relay: {okResponse.CommunicatorName} - {okResponse.Message}"));
                            return;
                        }

                        tcs.SetResult(Result.Success(okResponse.EventId!));
                    });

                return await tcs.Task;
            }

            private async Task<Result<TransactionInfo>> CreatProjectTransaction(
                string walletId,
                WalletWords words,
                long selectedFee,
                string founderKey,
                string projectIdentifier,
                string projectInfoEventId)
            {
                var accountBalanceInfo = await RefreshWalletBalance(walletId);
                if (accountBalanceInfo.IsFailure)
                {
                    logger.LogDebug("Failed to refresh account balance for WalletId {WalletId}: {Error}",
                        walletId, accountBalanceInfo.Error);
                    throw new InvalidOperationException("Failed to get account balance information");
                }

                var accountInfo = accountBalanceInfo.Value.AccountInfo;

                var unsignedTransaction = founderTransactionActions.CreateNewProjectTransaction(
                    founderKey,
                    derivationOperations.AngorKeyToScript(projectIdentifier),
                    NetworkConfiguration.AngorCreateFeeSats,
                    NetworkConfiguration.NostrEventIdKeyType,
                    projectInfoEventId);

                var signedTransaction = walletOperations.AddInputsAndSignTransaction(
                    accountInfo.GetNextChangeReceiveAddress()!,
                    unsignedTransaction,
                    words,
                    accountInfo,
                    selectedFee);

                return Result.Success(signedTransaction);
            }

            private async Task<Result<AccountBalanceInfo>> RefreshWalletBalance(string walletId)
            {
                try
                {
                    var dbResult = await walletAccountBalanceService.GetAccountBalanceInfoAsync(walletId);
                    if (dbResult.IsFailure)
                        return Result.Failure<AccountBalanceInfo>(dbResult.Error);

                    var refreshResult = await walletAccountBalanceService.RefreshAccountBalanceInfoAsync(walletId);
                    if (refreshResult.IsFailure)
                        return Result.Failure<AccountBalanceInfo>(refreshResult.Error);

                    return Result.Success(refreshResult.Value);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error refreshing balance for wallet {WalletId}", walletId);
                    return Result.Failure<AccountBalanceInfo>($"Error refreshing balance: {e.Message}");
                }
            }
        }
    }
}

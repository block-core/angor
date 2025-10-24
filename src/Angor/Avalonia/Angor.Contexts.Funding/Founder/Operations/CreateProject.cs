using Angor.Contests.CrossCutting;
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

// Add this using

namespace Angor.Contexts.Funding.Founder.Operations;

internal static class CreateProjectConstants
{
    public static class CreateProject
    {
        public record CreateProjectRequest(Guid WalletId, long SelectedFeeRate, CreateProjectDto Project)
            : IRequest<Result<TransactionDraft>>;

        public class CreateProjectHandler(
            ISeedwordsProvider seedwordsProvider,
            IDerivationOperations derivationOperations,
            IRelayService relayService,
            IFounderTransactionActions founderTransactionActions,
            IWalletOperations walletOperations,
            IIndexerService indexerService,
            INetworkConfiguration networkConfiguration,
            IWalletAccountBalanceService walletAccountBalanceService,
            ILogger<CreateProjectHandler> logger // Inject logger
        ) : IRequestHandler<CreateProjectRequest, Result<TransactionDraft>>
        {
            public async Task<Result<TransactionDraft>> Handle(CreateProjectRequest request, CancellationToken cancellationToken)
            {

                var wallet = await seedwordsProvider.GetSensitiveData(request.WalletId);

                var keys = derivationOperations.DeriveProjectKeys(wallet.Value.ToWalletWords(),
                    NetworkConfiguration.AngorTestKey); //TODO we need to get the key based on the selected network

                FounderKeys? newProjectKeys = null;

                foreach (var founderKeys in keys.Keys)
                {
                    var project = await indexerService.GetProjectByIdAsync(founderKeys.ProjectIdentifier);

                    if (project != null) continue;

                    newProjectKeys = founderKeys;
                    break;
                }

                if (newProjectKeys == null)
                {
                    return Result.Failure<TransactionDraft>("Failed to derive project keys");
                }

                var nostrPrivateKey =
                    await derivationOperations.DeriveProjectNostrPrivateKeyAsync(wallet.Value.ToWalletWords(),
                        newProjectKeys.FounderKey);
                var nostrKeyHex = Encoders.Hex.EncodeData(nostrPrivateKey.ToBytes());

                var profileCreateEvent = await CreatNostrProfileAsync(nostrKeyHex, request.Project);


                if (profileCreateEvent.IsFailure)
                {
                    return Result.Failure<TransactionDraft>(profileCreateEvent.Error);
                }

                var projectInfo =
                    await CreatProjectInfoOnNostr(nostrKeyHex, request.Project, newProjectKeys);

                if (projectInfo.IsFailure)
                {
                    // await relayService.DeleteProjectAsync(resultId,
                    //     nostrKeyHex); //TODO need to check if relays support the delete operation
                    
                    return Result.Failure<TransactionDraft>(projectInfo.Error);
                }

                var transactionInfo = await CreatProjectTransaction(request.WalletId, wallet.Value.ToWalletWords(),
                    request.SelectedFeeRate,
                    newProjectKeys.FounderKey, newProjectKeys.ProjectIdentifier, projectInfo.Value);

                if (transactionInfo.IsFailure)
                {
                    return Result.Failure<TransactionDraft>(transactionInfo.Error);
                }
                
                
                var response = await walletOperations.PublishTransactionAsync(networkConfiguration.GetNetwork(), transactionInfo.Value.Transaction);

                if (!response.Success)
                {
                    return Result.Failure<TransactionDraft>(response.Message);
                }

                var transactionId = transactionInfo.Value.Transaction.GetHash().ToString();

                return Result.Success(new TransactionDraft
                {
                    SignedTxHex = transactionInfo.Value.Transaction.ToHex(),
                    TransactionId = transactionId,
                    TransactionFee = new Amount(transactionInfo.Value.TransactionFee)
                });

                // TODO
                // project.CreationTransactionId = transactionId;
                // storage.UpdateFounderProject(project);
            }


            private async Task<Result<string>> CreatNostrProfileAsync(string nostrKey, CreateProjectDto project)
            {
                var tcs = new TaskCompletionSource<Result<string>>();

                var nostrMetadata = new NostrMetadata
                {
                    Name = project.ProjectName, Website = project.WebsiteUri, About = project.Description,
                    Picture = project.AvatarUri, Banner = project.BannerUri, Nip05 = project.Nip05,
                    Lud16 = project.Lud16, Nip57 = project.Nip57
                };

                var resultId = await relayService.CreateNostrProfileAsync(
                    nostrMetadata,
                    nostrKey,
                    okResponse =>
                    {
                        if (!okResponse.Accepted)
                        {
                            tcs.SetResult(Result.Failure<string>(
                                "Failed to store the project information on the relay!!! {_.CommunicatorName} - {_.Message}"));
                            return;
                        }

                        relayService.PublishNip65List(nostrKey, nip65OkResponse =>
                        {
                            if(tcs.Task.IsCompleted)
                                return;
                            
                            tcs.SetResult(!nip65OkResponse.Accepted
                                ? Result.Failure<string>("Failed to publish NIP-65 list")
                                : Result.Success(okResponse.EventId!));
                        });
                    });

                return await tcs.Task;
            }


            private async Task<Result<string>> CreatProjectInfoOnNostr(string nostrKeyHex, CreateProjectDto project,
                FounderKeys founderKeys)
            {
                var tsc = new TaskCompletionSource<Result<string>>();

                var projectInfo = new ProjectInfo
                {
                    FounderKey = founderKeys.FounderKey,
                    EndDate = project.EndDate ?? throw new InvalidOperationException("End date is required"),
                    StartDate = project.StartDate,
                    ExpiryDate = project.ExpiryDate ?? project.Stages.OrderByDescending(x => x.startDate).First()
                        .startDate.AddMonths(2).ToDateTime(TimeOnly.MinValue),
                    FounderRecoveryKey = founderKeys.FounderRecoveryKey,
                    NostrPubKey = founderKeys.NostrPubKey,
                    PenaltyDays = project.PenaltyDays,
                    ProjectIdentifier = founderKeys.ProjectIdentifier,
                    TargetAmount = project.TargetAmount.Sats,
                    Stages = project.Stages.Select(stage => new Stage()
                    {
                        AmountToRelease = stage.PercentageOfTotal * 100,
                        ReleaseDate = stage.startDate.ToDateTime(TimeOnly.MinValue),
                    }).ToList()
                };

                var resultId = await relayService.AddProjectAsync(projectInfo, nostrKeyHex,
                    okResponse =>
                    {
                        if (!okResponse.Accepted)
                        {
                            tsc.SetResult(Result.Failure<string>(
                                "Failed to store the project information on the relay!!! {_.CommunicatorName} - {_.Message}"));
                            return ;
                        }

                        // TODO _Logger.LogInformation($"Communicator {_.CommunicatorName} accepted event {_.EventId}");

                        // TODO project.ProjectInfoEventId = _.EventId;
                        tsc.SetResult(Result.Success(okResponse.EventId!));
                    });

                return await tsc.Task;
            }


            private async Task<Result<TransactionInfo>> CreatProjectTransaction(Guid walletId, WalletWords words,
                long selectedFee,
                string founderKey,
                string projectIdentifier, string projectInfoEventId)
            {
                var accountBalanceInfo = await RefreshWalletBalance(walletId);
                if (accountBalanceInfo.IsFailure)
                {
                    throw new InvalidOperationException("Failed to get account balance information");
                }

                var accountInfo = accountBalanceInfo.Value.AccountInfo;


                var unsignedTransaction = founderTransactionActions.CreateNewProjectTransaction(founderKey,
                    derivationOperations.AngorKeyToScript(projectIdentifier), NetworkConfiguration.AngorCreateFeeSats,
                    NetworkConfiguration.NostrEventIdKeyType, projectInfoEventId);

                var signedTransaction = walletOperations.AddInputsAndSignTransaction(
                    accountInfo.GetNextChangeReceiveAddress()!, unsignedTransaction, words, accountInfo,
                    selectedFee);

                return Result.Success(signedTransaction);

            }

            private async Task<Result<AccountBalanceInfo>> RefreshWalletBalance(Guid walletId)
            {
                try
                {
                    // Try to get from service first
                    var dbResult = await walletAccountBalanceService.GetAccountBalanceInfoAsync(walletId);

                    if (dbResult.IsFailure)
                        return Result.Failure<AccountBalanceInfo>(dbResult.Error);

                    // Refresh the existing balance
                    var refreshResult = await walletAccountBalanceService.RefreshAccountBalanceInfoAsync(walletId);

                    return refreshResult.IsFailure 
                        ? Result.Failure<AccountBalanceInfo>(refreshResult.Error) 
                        : Result.Success(refreshResult.Value);
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
using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;
using Nostr.Client.Messages.Metadata;
using Stage = Angor.Shared.Models.Stage;

namespace Angor.Contexts.Funding.Founder.Operations;

internal static class CreateProjectConstants
{
    public static class CreateProject
    {
        public record CreateProjectRequest(Guid WalletId, long SelectedFeeRate, CreateProjectDto Project)
            : IRequest<Result<string>>;

        public class CreateProjectHandler(
            ISeedwordsProvider seedwordsProvider,
            IDerivationOperations derivationOperations,
            IRelayService relayService,
            IFounderTransactionActions founderTransactionActions,
            IWalletOperations walletOperations,
            IIndexerService indexerService,
            INetworkConfiguration networkConfiguration) : IRequestHandler<CreateProjectRequest, Result<string>>
        {
            public async Task<Result<string>> Handle(CreateProjectRequest request, CancellationToken cancellationToken)
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
                    return Result.Failure<string>("Failed to derive project keys");
                }

                var nostrPrivateKey =
                    await derivationOperations.DeriveProjectNostrPrivateKeyAsync(wallet.Value.ToWalletWords(),
                        newProjectKeys.FounderKey);
                var nostrKeyHex = Encoders.Hex.EncodeData(nostrPrivateKey.ToBytes());

                var profileCreateEvent = await CreatNostrProfileAsync(nostrKeyHex, request.Project);


                if (profileCreateEvent.IsFailure)
                {
                    return Result.Failure<string>(profileCreateEvent.Error);
                }

                var projectInfo =
                    await CreatProjectInfoOnNostr(nostrKeyHex, request.Project, newProjectKeys);

                if (projectInfo.IsFailure)
                {
                    // await relayService.DeleteProjectAsync(resultId,
                    //     nostrKeyHex); //TODO need to check if relays support the delete operation
                    
                    return Result.Failure<string>(projectInfo.Error);
                }

                var transactionInfo = await CreatProjectTransaction(wallet.Value.ToWalletWords(),
                    request.SelectedFeeRate,
                    newProjectKeys.FounderKey, newProjectKeys.ProjectIdentifier, projectInfo.Value);

                if (transactionInfo.IsFailure)
                {
                    return Result.Failure<string>(transactionInfo.Error);
                }
                
                
                var response = await walletOperations.PublishTransactionAsync(networkConfiguration.GetNetwork(), transactionInfo.Value.Transaction);

                if (!response.Success)
                {
                    return Result.Failure<string>(response.Message);
                }

                var transactionId = transactionInfo.Value.Transaction.GetHash().ToString();
                

                // TODO
                // project.CreationTransactionId = transactionId;
                // storage.UpdateFounderProject(project);
                
                return Result.Success(transactionId);
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


            private async Task<Result<TransactionInfo>> CreatProjectTransaction(WalletWords words, long selectedFee,
                string founderKey,
                string projectIdentifier, string projectInfoEventId)
            {
                var accountBalanceInfo = await RefreshWalletBalance(words);
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

            private async Task<Result<AccountBalanceInfo>> RefreshWalletBalance(WalletWords walletWords)
            {
                try
                {
                    var accountInfo = walletOperations.BuildAccountInfoForWalletWords(walletWords);

                    await walletOperations.UpdateDataForExistingAddressesAsync(accountInfo);
                    await walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);

                    var accountBalanceInfo = new AccountBalanceInfo();

                    accountBalanceInfo.UpdateAccountBalanceInfo(accountInfo, []);

                    return Result.Success<AccountBalanceInfo>(accountBalanceInfo);
                }
                catch (Exception e)
                {
                    return Result.Failure<AccountBalanceInfo>($"Error refreshing balance: {e.Message}");
                }
            }
        }
    }
}
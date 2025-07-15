using Angor.Contests.CrossCutting;
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
using Stage = Angor.Shared.Models.Stage;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class PublishInvestment
{
    public record PublishInvestmentRequest(string InvestmentId, Guid WalletId, ProjectId ProjectId) : IRequest<Result>{}

    public class PublishInvestmentHandler(
        INetworkConfiguration networkConfiguration,
        ISignService signService,
        IEncryptionService decrypter,
        ISerializer serializer,
        IInvestorTransactionActions investorTransactionActions,
        IDerivationOperations derivationOperations,
        ISeedwordsProvider seedwordsProvider,
        IProjectRepository projectRepository,
        IWalletOperations walletOperations,
        ILogger logger) : IRequestHandler<PublishInvestmentRequest, Result>
    {
        public async Task<Result> Handle(PublishInvestmentRequest request, CancellationToken cancellationToken)
        {
            var projectResult = await projectRepository.Get(request.ProjectId);

            if (projectResult.IsFailure)
                return projectResult;

            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(request.WalletId);
            var pubKey =
                derivationOperations.DeriveNostrPubKey(sensitiveDataResult.Value.ToWalletWords(),
                    projectResult.Value.FounderKey);
            var nostrPrivateKey =
                await derivationOperations.DeriveProjectNostrPrivateKeyAsync(sensitiveDataResult.Value.ToWalletWords(),
                    projectResult.Value.FounderKey);

            var privateKeyHex = Encoders.Hex.EncodeData(nostrPrivateKey.ToBytes());

            var investmentTransactionResult = await GetInvestmentTransactionFromSignatureRequestAsync(
                request.InvestmentId, pubKey, privateKeyHex, projectResult.Value.NostrPubKey);

            var validate = await ValidateFounderSignatures(projectResult.Value,
                investmentTransactionResult.Value.createTime,
                investmentTransactionResult.Value.evenbId, investmentTransactionResult.Value.transaction, pubKey,
                privateKeyHex,
                projectResult.Value.NostrPubKey);

            if (validate.IsFailure)
                return validate;
            
            return await PublishSignedTransactionAsync(investmentTransactionResult.Value.transaction);
        }


        private async Task<Result<(TransactionInfo transaction, DateTime createTime, string evenbId)>>
            GetInvestmentTransactionFromSignatureRequestAsync(string transactionHash, string senderPubKey,
                string privateKeyHex, string projectPubKey)
        {
            var createdAt = DateTime.MinValue;
            var eventId = string.Empty;

            TransactionInfo investment = null;
            var tcs = new TaskCompletionSource<Result<bool>>();


            // TODO replace the old logic with better optimized one
            await signService.LookupInvestmentRequestsAsync(projectPubKey, senderPubKey, null,
                async (id, publisherPubKey, content, eventTime) =>
                {
                    if (createdAt >= eventTime) return;

                    createdAt = eventTime;
                    eventId = id;

                    try
                    {
                        var decrypted =
                            await decrypter.DecryptNostrContentAsync(privateKeyHex, publisherPubKey, content);

                        var investmentRequest = serializer.Deserialize<SignRecoveryRequest>(decrypted);
                        var trx = networkConfiguration.GetNetwork()
                            .CreateTransaction(investmentRequest.InvestmentTransactionHex);

                        if (trx.GetHash().ToString() == transactionHash)
                            investment = new TransactionInfo() { Transaction = trx };
                    }
                    catch (Exception e)
                    {
                        tcs.TrySetResult(Result.Failure<bool>(e.Message));
                    }
                }, () =>
                {
                    tcs.TrySetResult(
                        investment == null
                            ? Result.Failure<bool>("Investment transaction not found")
                            : Result.Success(true));
                }
            );

            await tcs.Task;

            return tcs.Task.Result.IsFailure
                ? Result.Failure<(TransactionInfo, DateTime, string)>(tcs.Task.Result.Error)
                : tcs.Task.Result.Value
                    ? Result.Success((investment!, createdAt, eventId))
                    : Result.Failure<(TransactionInfo, DateTime, string)>(
                        "Failed to retrieve investment transaction signatures");
        }


        private async Task<Result<bool>> ValidateFounderSignatures(Project project, DateTime createdAt, string eventId,
            TransactionInfo investment,
            string senderPubKey, string privateKeyHex, string projectPubKey)
        {
            var signatureInfo = new SignatureInfo();
            var tcs = new TaskCompletionSource<Result<bool>>();

            signService.LookupSignatureForInvestmentRequest(senderPubKey, projectPubKey, createdAt, eventId,
                async content =>
                {
                    var signatures =
                        await decrypter.DecryptNostrContentAsync(privateKeyHex, projectPubKey, content);

                    signatureInfo = serializer.Deserialize<SignatureInfo>(signatures);

                    var validSignatures =
                        investorTransactionActions.CheckInvestorRecoverySignatures(MapToProjectInfo(project),
                            investment.Transaction, signatureInfo);

                    tcs.SetResult(Result.Success(validSignatures));
                },
                () => { if (!tcs.Task.IsCompleted) tcs.TrySetResult(Result.Success(false));});


            await tcs.Task;

            return tcs.Task.Result;
        }

        private static ProjectInfo MapToProjectInfo(Project project)
        {
            return new ProjectInfo
            {
                FounderKey = project.FounderKey,
                NostrPubKey = project.NostrPubKey,
                ProjectIdentifier = project.Id.Value,
                EndDate = project.EndDate,
                ExpiryDate = project.ExpiryDate,
                FounderRecoveryKey = project.FounderRecoveryKey,
                PenaltyDays = project.PenaltyDuration.Days,
                Stages = project.Stages.Select(x => new Stage
                {
                    AmountToRelease = x.Amount,
                    ReleaseDate = x.ReleaseDate
                }).ToList(),
                StartDate = project.StartingDate,
                TargetAmount = project.TargetAmount,
            };
        }


        private async Task<Result<string>> PublishSignedTransactionAsync(TransactionInfo signedTransaction)
        {
            try
            { 
                var response = await walletOperations.PublishTransactionAsync(networkConfiguration.GetNetwork(),
                    signedTransaction.Transaction);
                
                if (response.Success)
                    return Result.Success(signedTransaction.Transaction.GetHash().ToString());
                
                logger.LogError(response.Message);
                
                return Result.Failure<string>("Failed to publish the transaction to the blockchain");
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error publishing signed transaction");
                return Result.Failure<string>("An error occurred while publishing the transaction");
            }
        }
    }
}
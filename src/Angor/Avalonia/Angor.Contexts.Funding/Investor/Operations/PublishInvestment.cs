using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Investor.Domain;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;
using Serilog;
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
        IPortfolioRepository  investmentRepository,
        ILogger logger) : IRequestHandler<PublishInvestmentRequest, Result>
    {
        public async Task<Result> Handle(PublishInvestmentRequest request, CancellationToken cancellationToken)
        {
            var projectResult = await projectRepository.GetAsync(request.ProjectId);

            if (projectResult.IsFailure)
                return projectResult;

            var portfolio = await investmentRepository.GetByWalletId(request.WalletId);
            
            if (portfolio.IsFailure)
                return Result.Failure(portfolio.Error);
            
            var investmentRecord = portfolio.Value.ProjectIdentifiers
                .FirstOrDefault(x => x.ProjectIdentifier == request.ProjectId.Value);
            
            if (investmentRecord?.InvestmentTransactionHex == null || investmentRecord.RequestEventId == null || investmentRecord.RequestEventTime == null)
                return Result.Failure("The investment transaction was not found in storage");

            if (investmentRecord.InvestmentTransactionHash != request.InvestmentId)
                return Result.Failure("Failed to find the investment transaction with the given ID");
            
            var transactionInfo = new TransactionInfo()
            {
                Transaction = networkConfiguration.GetNetwork()
                    .CreateTransaction(investmentRecord.InvestmentTransactionHex)
            };
            
            var validate = await ValidateFounderSignatures(request.WalletId, projectResult.Value,
                investmentRecord.RequestEventTime.Value, investmentRecord.RequestEventId, transactionInfo, 
                projectResult.Value.NostrPubKey);

            if (validate.IsFailure)
                return validate;
            
            return await PublishSignedTransactionAsync(transactionInfo);
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


        private async Task<Result<bool>> ValidateFounderSignatures(Guid walletId, Project project, DateTime createdAt, string eventId,
            TransactionInfo investment,string projectPubKey)
        {
            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(walletId);
            var pubKey =
                derivationOperations.DeriveNostrPubKey(sensitiveDataResult.Value.ToWalletWords(),
                    project.FounderKey);
            var nostrPrivateKey =
                await derivationOperations.DeriveProjectNostrPrivateKeyAsync(sensitiveDataResult.Value.ToWalletWords(),
                    project.FounderKey);

            var privateKeyHex = Encoders.Hex.EncodeData(nostrPrivateKey.ToBytes());
            
            var signatureInfo = new SignatureInfo();
            var tcs = new TaskCompletionSource<Result<bool>>();

            signService.LookupSignatureForInvestmentRequest(pubKey, projectPubKey, createdAt, eventId,
                async content =>
                {
                    var signatures =
                        await decrypter.DecryptNostrContentAsync(privateKeyHex, projectPubKey, content);

                    signatureInfo = serializer.Deserialize<SignatureInfo>(signatures);

                    var validSignatures =
                        investorTransactionActions.CheckInvestorRecoverySignatures(project.ToProjectInfo(),
                            investment.Transaction, signatureInfo);

                    //TODO do we need to store the signatures in the database at this point?
                    
                    tcs.SetResult(Result.Success(validSignatures));
                },
                () => { if (!tcs.Task.IsCompleted) tcs.TrySetResult(Result.Success(false));});


            await tcs.Task;

            return tcs.Task.Result;
        }

        // private static ProjectInfo MapToProjectInfo(Project project)
        // {
        //     return new ProjectInfo
        //     {
        //         FounderKey = project.FounderKey,
        //         NostrPubKey = project.NostrPubKey,
        //         ProjectIdentifier = project.Id.Value,
        //         EndDate = project.EndDate,
        //         ExpiryDate = project.ExpiryDate,
        //         FounderRecoveryKey = project.FounderRecoveryKey,
        //         PenaltyDays = project.PenaltyDuration.Days,
        //         Stages = project.Stages.Select(x => new Stage
        //         {
        //             AmountToRelease = x.RatioOfTotal * 100,
        //             ReleaseDate = x.ReleaseDate
        //         }).ToList(),
        //         StartDate = project.StartingDate,
        //         TargetAmount = project.TargetAmount,
        //     };
        // }


        private async Task<Result<string>> PublishSignedTransactionAsync(TransactionInfo signedTransaction)
        {
            try
            { 
                var response = await walletOperations.PublishTransactionAsync(networkConfiguration.GetNetwork(),
                    signedTransaction.Transaction);
                
                if (response.Success)
                    return Result.Success(signedTransaction.Transaction.GetHash().ToString());
                
                logger.Error(response.Message);
                
                return Result.Failure<string>("Failed to publish the transaction to the blockchain");
            }
            catch (Exception e)
            {
                logger.Error(e, "Error publishing signed transaction");
                return Result.Failure<string>("An error occurred while publishing the transaction");
            }
        }
    }
}
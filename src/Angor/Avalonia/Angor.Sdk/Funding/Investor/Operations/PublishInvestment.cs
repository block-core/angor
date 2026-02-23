using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
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

namespace Angor.Sdk.Funding.Investor.Operations;

public static class PublishInvestment
{
    public record PublishInvestmentRequest(string InvestmentId, WalletId WalletId, ProjectId ProjectId) : IRequest<Result<PublishInvestmentResponse>>;

    public record PublishInvestmentResponse();

    public class PublishInvestmentHandler(
        INetworkConfiguration networkConfiguration,
        ISignService signService,
        IEncryptionService decrypter,
        ISerializer serializer,
        IInvestorTransactionActions investorTransactionActions,
        IDerivationOperations derivationOperations,
        ISeedwordsProvider seedwordsProvider,
        IProjectService projectService,
        IWalletOperations walletOperations,
        IPortfolioService investmentService,
        IWalletAccountBalanceService walletAccountBalanceService,
        ILogger logger) : IRequestHandler<PublishInvestmentRequest, Result<PublishInvestmentResponse>>
    {
        public async Task<Result<PublishInvestmentResponse>> Handle(PublishInvestmentRequest request, CancellationToken cancellationToken)
        {
            var projectResult = await projectService.GetAsync(request.ProjectId);

            if (projectResult.IsFailure)
                return Result.Failure<PublishInvestmentResponse>(projectResult.Error);

            var portfolio = await investmentService.GetByWalletId(request.WalletId.Value);
            
            if (portfolio.IsFailure)
                return Result.Failure<PublishInvestmentResponse>(portfolio.Error);
            
            var investmentRecord = portfolio.Value.ProjectIdentifiers
                .FirstOrDefault(x => x.ProjectIdentifier == request.ProjectId.Value);
            
            if (investmentRecord?.InvestmentTransactionHex == null || investmentRecord.RequestEventId == null || investmentRecord.RequestEventTime == null)
                return Result.Failure<PublishInvestmentResponse>("The investment transaction was not found in storage");

            if (investmentRecord.InvestmentTransactionHash != request.InvestmentId)
                return Result.Failure<PublishInvestmentResponse>("Failed to find the investment transaction with the given ID");
            
            var transactionInfo = new TransactionInfo()
            {
                Transaction = networkConfiguration.GetNetwork()
                    .CreateTransaction(investmentRecord.InvestmentTransactionHex)
            };
            
            var validate = await ValidateFounderSignatures(request.WalletId.Value, projectResult.Value,
                investmentRecord.RequestEventTime.Value, investmentRecord.RequestEventId, transactionInfo, 
                projectResult.Value.NostrPubKey);

            if (validate.IsFailure)
                return Result.Failure<PublishInvestmentResponse>(validate.Error);
            
            var publishResult = await PublishSignedTransactionAsync(transactionInfo);

            if (publishResult.IsFailure)
                return Result.Failure<PublishInvestmentResponse>(publishResult.Error);

            // After successful broadcast, update UTXO state
            await UpdateUtxoStateAfterPublish(request.WalletId, transactionInfo);
    
            return Result.Success(new PublishInvestmentResponse());
        }

        private async Task UpdateUtxoStateAfterPublish(WalletId walletId, TransactionInfo transactionInfo)
        {
            var network = networkConfiguration.GetNetwork();
            var transaction = transactionInfo.Transaction;

            var accountBalanceResult = await walletAccountBalanceService.GetAccountBalanceInfoAsync(walletId);
            if (accountBalanceResult.IsFailure)
                return;

            var accountBalanceInfo = accountBalanceResult.Value;
            var accountInfo = accountBalanceInfo.AccountInfo;
            var transactionHash = transaction.GetHash().ToString();

            // Remove from reserved and mark as pending spent (following WalletOperations.UpdateAccountUnconfirmedInfoWithSpentTransaction pattern)
            var inputs = transaction.Inputs.Select(input => input.PrevOut.ToString()).ToList();

            foreach (var utxoData in accountInfo.AllUtxos())
            {
                // Find all spent inputs to mark them as spent
                if (inputs.Contains(utxoData.outpoint.ToString()))
                    utxoData.PendingSpent = true;
            }

            // Remove from UtxoReservedForInvestment
            foreach (var input in transaction.Inputs)
            {
                var outpointString = input.PrevOut.ToString();
                accountInfo.UtxoReservedForInvestment.Remove(outpointString);
            }

            // Collect change outputs as pending receive UTXOs
            var accountChangeAddresses = accountInfo.ChangeAddressesInfo.Select(x => x.Address).ToList();
            var pendingReceiveUtxos = new List<UtxoData>();

            foreach (var output in transaction.Outputs.AsIndexedOutputs())
            {
                var address = output.TxOut.ScriptPubKey.GetDestinationAddress(network)?.ToString();

                if (address != null && accountChangeAddresses.Contains(address))
                {
                    pendingReceiveUtxos.Add(new UtxoData
                    {
                        address = address,
                        scriptHex = output.TxOut.ScriptPubKey.ToHex(),
                        outpoint = new Outpoint(transactionHash, (int)output.N),
                        blockIndex = 0,
                        value = output.TxOut.Value
                    });
                }
            }

            // Add pending receive UTXOs to AccountPendingReceive
            accountBalanceInfo.AccountPendingReceive.AddRange(pendingReceiveUtxos);

            await walletAccountBalanceService.SaveAccountBalanceInfoAsync(walletId, accountBalanceInfo);
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


        private async Task<Result<bool>> ValidateFounderSignatures(string walletId, Project project, DateTime createdAt, string eventId,
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
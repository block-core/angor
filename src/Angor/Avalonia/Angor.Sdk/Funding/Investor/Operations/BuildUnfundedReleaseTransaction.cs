using Angor.Sdk.Common;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Funding.Shared.TransactionDrafts;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;
using MoreLinq.Extensions;
using Angor.Sdk.Funding.Projects;

namespace Angor.Sdk.Funding.Investor.Operations;

public static class BuildUnfundedReleaseTransaction
{
    public record BuildUnfundedReleaseTransactionRequest(WalletId WalletId, ProjectId ProjectId, DomainFeerate SelectedFeeRate) : IRequest<Result<BuildUnfundedReleaseTransactionResponse>>;
    
    public record BuildUnfundedReleaseTransactionResponse(UnfundedReleaseTransactionDraft TransactionDraft);
    
    public class BuildUnfundedReleaseTransactionHandler(ISeedwordsProvider provider, IDerivationOperations derivationOperations,
        IProjectService projectService, IInvestorTransactionActions investorTransactionActions,
        IPortfolioService investmentService, INetworkConfiguration networkConfiguration,
        IWalletOperations walletOperations, ISignService signService,
        IEncryptionService decrypter, ISerializer serializer,
        ITransactionService transactionService,
        IWalletAccountBalanceService walletAccountBalanceService) : IRequestHandler<BuildUnfundedReleaseTransactionRequest, Result<BuildUnfundedReleaseTransactionResponse>>
    {
        public async Task<Result<BuildUnfundedReleaseTransactionResponse>> Handle(BuildUnfundedReleaseTransactionRequest request, CancellationToken cancellationToken)
        {
            var project = await projectService.GetAsync(request.ProjectId);
            if (project.IsFailure)
                return Result.Failure<BuildUnfundedReleaseTransactionResponse>(project.Error);
            
            var investments = await investmentService.GetByWalletId(request.WalletId.Value);
            if (investments.IsFailure)
                return Result.Failure<BuildUnfundedReleaseTransactionResponse>(investments.Error);
            
            var investment = investments.Value.ProjectIdentifiers.FirstOrDefault(p => p.ProjectIdentifier == request.ProjectId.Value);
            if (investment is null) //TODO we need to make sure we always have this data
                return Result.Failure<BuildUnfundedReleaseTransactionResponse>("No investment found for this project");

            var words = await provider.GetSensitiveData(request.WalletId.Value);
            if (words.IsFailure)
                return Result.Failure<BuildUnfundedReleaseTransactionResponse>(words.Error);
            
            // Get account info from database
            var accountBalanceResult = await walletAccountBalanceService.GetAccountBalanceInfoAsync(request.WalletId);
            if (accountBalanceResult.IsFailure)
                return Result.Failure<BuildUnfundedReleaseTransactionResponse>(accountBalanceResult.Error);
            
            var accountInfo = accountBalanceResult.Value.AccountInfo;

            var investorPrivateKey = derivationOperations.DeriveInvestorPrivateKey(words.Value.ToWalletWords(), project.Value.FounderKey);

            var investmentTransaction = networkConfiguration.GetNetwork().CreateTransaction(investment.InvestmentTransactionHex);

            var signatureLookup = await LookupFounderReleaseSignatures(request.WalletId.Value, project.Value, investment.RequestEventId);
            
            if (signatureLookup.IsFailure)
                return Result.Failure<BuildUnfundedReleaseTransactionResponse>(signatureLookup.Error ?? "Could not retrieve founder signatures");
            
            if (signatureLookup.Value is null)
                return Result.Failure<BuildUnfundedReleaseTransactionResponse>("No founder signatures found");
            
            var investorReleaseSigInfo = signatureLookup.Value;
            
            // Sign the release transaction
            var unsignedReleaseTransaction = investorTransactionActions.AddSignaturesToUnfundedReleaseFundsTransaction(project.Value.ToProjectInfo(), investmentTransaction, investorReleaseSigInfo, Encoders.Hex.EncodeData(investorPrivateKey.ToBytes()), investment.UnfundedReleaseAddress);

            // Validate the signatures
            var sigCheckResult = investorTransactionActions.CheckInvestorUnfundedReleaseSignatures(project.Value.ToProjectInfo(), investmentTransaction, investorReleaseSigInfo, investment.UnfundedReleaseAddress);

            if (!sigCheckResult)
                throw new Exception("Failed to validate signatures");

            var transactionInfo = await transactionService.GetTransactionInfoByIdAsync(investmentTransaction.GetHash().ToString());

            if (transactionInfo is null)
                return Result.Failure<BuildUnfundedReleaseTransactionResponse>("Could not find transaction info");

            transactionInfo.Outputs.ForEach((output, i) =>
            {
                if (i < 2 || string.IsNullOrEmpty(output.SpentInTransaction))
                    return;

                unsignedReleaseTransaction.Inputs.RemoveAt(i - 2);
                unsignedReleaseTransaction.Outputs.RemoveAt(i - 2);
            });
            
            var changeAddress = accountInfo.GetNextChangeReceiveAddress();
            if (changeAddress == null)
                return Result.Failure<BuildUnfundedReleaseTransactionResponse>("Could not get a change address");
            
            // add fee to the recovery trx
            var releaseTransaction = walletOperations.AddFeeAndSignTransaction(changeAddress, unsignedReleaseTransaction, words.Value.ToWalletWords(), accountInfo, request.SelectedFeeRate.SatsPerKilobyte);
            
            return Result.Success(new BuildUnfundedReleaseTransactionResponse(new UnfundedReleaseTransactionDraft
            {
                SignedTxHex = releaseTransaction.Transaction.ToHex(),
                TransactionFee = new Amount(releaseTransaction.TransactionFee),
                TransactionId = releaseTransaction.Transaction.GetHash().ToString()
            }));
        }
        
        private async Task<Result<SignatureInfo?>> LookupFounderReleaseSignatures(string walletId, Project project, string eventId)
        {
            var sensitiveDataResult = await provider.GetSensitiveData(walletId);
            var pubKey = derivationOperations.DeriveNostrPubKey(sensitiveDataResult.Value.ToWalletWords(), project.FounderKey);
            var nostrPrivateKey = await derivationOperations.DeriveProjectNostrPrivateKeyAsync(sensitiveDataResult.Value.ToWalletWords(), project.FounderKey);

            var privateKeyHex = Encoders.Hex.EncodeData(nostrPrivateKey.ToBytes());
            
            var tcs = new TaskCompletionSource<Result<SignatureInfo?>>();

            var projectPubKey = project.NostrPubKey;
            
            signService.LookupReleaseSigs(pubKey, projectPubKey, null, eventId,
                async content =>
                {
                    var signatures =
                        await decrypter.DecryptNostrContentAsync(privateKeyHex, projectPubKey, content);

                    var signatureInfo = serializer.Deserialize<SignatureInfo>(signatures);

                    tcs.TrySetResult(Result.Success<SignatureInfo?>(signatureInfo));
                },
                () => { if (!tcs.Task.IsCompleted) tcs.TrySetResult(result: Result.Success<SignatureInfo?>(null));});


            await tcs.Task;

            return tcs.Task.Result;
        }
    }
}

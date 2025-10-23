using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Investor.Domain;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Shared.Repositories;
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

namespace Angor.Contexts.Funding.Investor.Operations;

public static class ReleaseFunds
{
    public record ReleaseFundsRequest(Guid WalletId, ProjectId ProjectId, DomainFeerate SelectedFeeRate) : IRequest<Result<TransactionDraft>>;
    
    public class ReleaseFundsHandler(ISeedwordsProvider provider, IDerivationOperations derivationOperations,
        IProjectRepository projectRepository, IInvestorTransactionActions investorTransactionActions,
        IPortfolioRepository investmentRepository, INetworkConfiguration networkConfiguration,
        IWalletOperations walletOperations, IIndexerService indexerService, ISignService signService,
        IEncryptionService decrypter, ISerializer serializer,
        ITransactionRepository transactionRepository) : IRequestHandler<ReleaseFundsRequest, Result<TransactionDraft>>
    {
        public async Task<Result<TransactionDraft>> Handle(ReleaseFundsRequest request, CancellationToken cancellationToken)
        {
            var project = await projectRepository.GetAsync(request.ProjectId);
            if (project.IsFailure)
                return Result.Failure<TransactionDraft>(project.Error);
            
            var investments = await investmentRepository.GetByWalletId(request.WalletId);
            if (investments.IsFailure)
                return Result.Failure<TransactionDraft>(investments.Error);
            
            var investment = investments.Value.ProjectIdentifiers.FirstOrDefault(p => p.ProjectIdentifier == request.ProjectId.Value);
            if (investment is null) //TODO we need to make sure we always have this data
                return Result.Failure<TransactionDraft>("No investment found for this project");

            var words = await provider.GetSensitiveData(request.WalletId);
            if (words.IsFailure)
                return Result.Failure<TransactionDraft>(words.Error);
            
            var accountInfo = walletOperations.BuildAccountInfoForWalletWords(words.Value.ToWalletWords());
            await walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);

            var investorPrivateKey = derivationOperations.DeriveInvestorPrivateKey(words.Value.ToWalletWords(), project.Value.FounderKey);

            var investmentTransaction = networkConfiguration.GetNetwork().CreateTransaction(investment.InvestmentTransactionHex);

            var signatureLookup = await LookupFounderReleaseSignatures(request.WalletId, project.Value, investment.RequestEventId, 
                investmentTransaction);
            
            if (signatureLookup.IsFailure)
                return Result.Failure<TransactionDraft>(signatureLookup.Error ?? "Could not retrieve founder signatures");
            
            if (signatureLookup.Value is null)
                return Result.Failure<TransactionDraft>("No founder signatures found");
            
            var investorReleaseSigInfo = signatureLookup.Value;
            
            // Sign the release transaction
            var unsignedReleaseTransaction = investorTransactionActions.AddSignaturesToUnfundedReleaseFundsTransaction(project.Value.ToProjectInfo(), investmentTransaction, investorReleaseSigInfo, Encoders.Hex.EncodeData(investorPrivateKey.ToBytes()), investment.UnfundedReleaseAddress);

            // Validate the signatures
            var sigCheckResult = investorTransactionActions.CheckInvestorUnfundedReleaseSignatures(project.Value.ToProjectInfo(), investmentTransaction, investorReleaseSigInfo, investment.UnfundedReleaseAddress);

            if (!sigCheckResult)
                throw new Exception("Failed to validate signatures");

            var transactionInfo = await transactionRepository.GetTransactionInfoByIdAsync(investmentTransaction.GetHash().ToString());

            if (transactionInfo is null)
                return Result.Failure<TransactionDraft>("Could not find transaction info");

            transactionInfo.Outputs.ForEach((output, i) =>
            {
                if (i < 2 || string.IsNullOrEmpty(output.SpentInTransaction))
                    return;

                unsignedReleaseTransaction.Inputs.RemoveAt(i - 2);
                unsignedReleaseTransaction.Outputs.RemoveAt(i - 2);
            });
            
            var changeAddress = accountInfo.GetNextChangeReceiveAddress();
            if (changeAddress == null)
                return Result.Failure<TransactionDraft>("Could not get a change address");
            
            // add fee to the recovery trx
            var releaseTransaction = walletOperations.AddFeeAndSignTransaction(changeAddress, unsignedReleaseTransaction, words.Value.ToWalletWords(), accountInfo, request.SelectedFeeRate.SatsPerKilobyte);
            
            //var transactionId = await indexerService.PublishTransactionAsync(releaseTransaction.Transaction.ToHex());

            return Result.Success(new TransactionDraft
            {
                SignedTxHex = releaseTransaction.Transaction.ToHex(),
                TransactionFee = new Amount(releaseTransaction.TransactionFee),
                TransactionId = releaseTransaction.Transaction.GetHash().ToString()
            });
        }
        
        private async Task<Result<SignatureInfo?>> LookupFounderReleaseSignatures(Guid walletId, Project project, string eventId,
            Transaction investment)
        {
            var sensitiveDataResult = await provider.GetSensitiveData(walletId);
            var pubKey =
                derivationOperations.DeriveNostrPubKey(sensitiveDataResult.Value.ToWalletWords(),
                    project.FounderKey);
            var nostrPrivateKey =
                await derivationOperations.DeriveProjectNostrPrivateKeyAsync(sensitiveDataResult.Value.ToWalletWords(),
                    project.FounderKey);

            var privateKeyHex = Encoders.Hex.EncodeData(nostrPrivateKey.ToBytes());
            
            var signatureInfo = new SignatureInfo();
            var tcs = new TaskCompletionSource<Result<SignatureInfo?>>();

            var projectPubKey = project.NostrPubKey;
            
            signService.LookupReleaseSigs(pubKey, projectPubKey, null, eventId,
                async content =>
                {
                    var signatures =
                        await decrypter.DecryptNostrContentAsync(privateKeyHex, projectPubKey, content);

                    signatureInfo = serializer.Deserialize<SignatureInfo>(signatures);

                    var validSignatures =
                        investorTransactionActions.CheckInvestorRecoverySignatures(project.ToProjectInfo(),
                            investment, signatureInfo);

                    tcs.SetResult(validSignatures
                        ? Result.Success<SignatureInfo?>(signatureInfo)
                        : Result.Failure<SignatureInfo?>("Invalid signatures"));
                },
                () => { if (!tcs.Task.IsCompleted) tcs.TrySetResult(result: Result.Success<SignatureInfo?>(null));});


            await tcs.Task;

            return tcs.Task.Result;
        }
    }
}


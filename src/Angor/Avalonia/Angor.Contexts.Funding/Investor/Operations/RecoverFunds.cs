using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Investor.Domain;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Shared.TransactionDrafts;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;
using MoreLinq;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class RecoverFunds
{
    public record RecoverFundsRequest(Guid WalletId, ProjectId ProjectId,DomainFeerate SelectedFeeRate) : IRequest<Result<TransactionDraft>>;

    // TODO: Placeholder handler
    public class RecoverFundsHandler(ISeedwordsProvider provider, IDerivationOperations derivationOperations,
        IProjectRepository projectRepository, IInvestorTransactionActions investorTransactionActions,
        IPortfolioRepository investmentRepository, INetworkConfiguration networkConfiguration,
        IWalletOperations walletOperations, IIndexerService indexerService, ISignService signService,
        IEncryptionService decrypter, ISerializer serializer) : IRequestHandler<RecoverFundsRequest, Result<TransactionDraft>>
    {
        public async Task<Result<TransactionDraft>> Handle(RecoverFundsRequest request, CancellationToken cancellationToken)
        {
            var words = await provider.GetSensitiveData(request.WalletId);
            if (words.IsFailure)
                return Result.Failure<TransactionDraft>(words.Error);
            
            var accountInfo = walletOperations.BuildAccountInfoForWalletWords(words.Value.ToWalletWords());
            await walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);
            
            var project = await projectRepository.GetAsync(request.ProjectId);
            if (project.IsFailure)
                return Result.Failure<TransactionDraft>(project.Error);
            var investments = await investmentRepository.GetByWalletId(request.WalletId);
            if (investments.IsFailure)
                return Result.Failure<TransactionDraft>(investments.Error);
            
            var investment = investments.Value.ProjectIdentifiers.FirstOrDefault(p => p.ProjectIdentifier == request.ProjectId.Value);
            if (investment is null)
                return Result.Failure<TransactionDraft>("No investment found for this project");
            
            var investorPrivateKey = derivationOperations.DeriveInvestorPrivateKey(words.Value.ToWalletWords(), project.Value.FounderKey);

            var investmentTransaction = networkConfiguration.GetNetwork().CreateTransaction(investment.InvestmentTransactionHex);
            
            var signatureLookup = await LookupFounderSignatures(request.WalletId, project.Value, investment.RequestEventTime.Value, investment.RequestEventId, 
                investmentTransaction);

            if (signatureLookup.IsFailure)
                return Result.Failure<TransactionDraft>(signatureLookup.Error ?? "Could not retrieve founder signatures");
            if (signatureLookup.Value is null)
                return Result.Failure<TransactionDraft>("No founder signatures found");
            
            
            var unsignedRecoveryTransaction = investorTransactionActions.AddSignaturesToRecoverSeederFundsTransaction(project.Value.ToProjectInfo(), investmentTransaction, signatureLookup.Value, Encoders.Hex.EncodeData(investorPrivateKey.ToBytes()));

            
            var transactionInfo = await indexerService.GetTransactionInfoByIdAsync(investmentTransaction.GetHash().ToString());

            if (transactionInfo is null)
                return Result.Failure<TransactionDraft>("Could not find transaction info");

            transactionInfo.Outputs.ForEach((output, i) =>
            {
                if (i < 2 || string.IsNullOrEmpty(output.SpentInTransaction))
                    return;

                unsignedRecoveryTransaction.Inputs.RemoveAt(i - 2);
                unsignedRecoveryTransaction.Outputs.RemoveAt(i - 2);
            });
            
            var changeAddress = accountInfo.GetNextChangeReceiveAddress();
            if (changeAddress == null)
                return Result.Failure<TransactionDraft>("Could not get a change address");
            
            // add fee to the recovery trx
            var recoveryTransaction = walletOperations.AddFeeAndSignTransaction(changeAddress, unsignedRecoveryTransaction, words.Value.ToWalletWords(), accountInfo, request.SelectedFeeRate.SatsPerKilobyte);

            return Result.Success(new TransactionDraft
            {
                SignedTxHex = recoveryTransaction.Transaction.ToHex(),
                TransactionFee = new Amount(recoveryTransaction.TransactionFee),
                TransactionId = recoveryTransaction.Transaction.GetHash().ToString()
            });
        }
        
        private async Task<Result<SignatureInfo?>> LookupFounderSignatures(Guid walletId, Project project, DateTime createdAt, string eventId,
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
            
            signService.LookupSignatureForInvestmentRequest(pubKey, projectPubKey, createdAt, eventId,
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


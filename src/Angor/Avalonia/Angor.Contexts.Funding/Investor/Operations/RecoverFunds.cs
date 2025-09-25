using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class RecoverFunds
{
    public record RecoverFundsRequest(Guid WalletId, ProjectId ProjectId, int StageIndex) : IRequest<Result>;

    // TODO: Placeholder handler
    public class RecoverFundsHandler(ISeedwordsProvider provider, IDerivationOperations derivationOperations,
        IProjectRepository projectRepository, IInvestorTransactionActions investorTransactionActions,
        IInvestmentRepository investmentRepository, INetworkConfiguration networkConfiguration,
        IWalletOperations walletOperations, IIndexerService indexerService, ISignService signService,
        IEncryptionService decrypter, ISerializer serializer) : IRequestHandler<RecoverFundsRequest, Result>
    {
        public async Task<Result> Handle(RecoverFundsRequest request, CancellationToken cancellationToken)
        {
            var fetchFees = await walletOperations.GetFeeEstimationAsync();
            var selectedFeeEstimation = fetchFees.OrderBy(x => x.FeeRate).First();
            
            var words = await provider.GetSensitiveData(request.WalletId);
            if (words.IsFailure)
                return Result.Failure(words.Error);
            
            var accountInfo = walletOperations.BuildAccountInfoForWalletWords(words.Value.ToWalletWords());
            await walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);
            
            var project = await projectRepository.Get(request.ProjectId);
            if (project.IsFailure)
                return Result.Failure(project.Error);
            var investments = await investmentRepository.GetByWalletId(request.WalletId);
            if (investments.IsFailure)
                return Result.Failure(investments.Error);
            
            var investment = investments.Value.ProjectIdentifiers.FirstOrDefault(p => p.ProjectIdentifier == request.ProjectId.Value);
            if (investment is null)
                return Result.Failure("No investment found for this project");
            
            var investorPrivateKey = derivationOperations.DeriveInvestorPrivateKey(words.Value.ToWalletWords(), project.Value.FounderKey);

            var investmentTransaction = networkConfiguration.GetNetwork().CreateTransaction(investment.InvestmentTransactionHex);
            
            var signatureLookup = await LookupFounderSignatures(request.WalletId, project.Value, investment.RequestEventTime.Value, investment.RequestEventId, 
                investmentTransaction, investorPrivateKey.PubKey.ToHex());

            if (signatureLookup.IsFailure || signatureLookup.Value is null)
                return Result.Failure(signatureLookup.Error ?? "Could not retrieve founder signatures");
            
            var unsignedRecoveryTransaction = investorTransactionActions.AddSignaturesToRecoverSeederFundsTransaction(project.Value.ToProjectInfo(), investmentTransaction, signatureLookup.Value, Encoders.Hex.EncodeData(investorPrivateKey.ToBytes()));

            // keep only the input and output for the selected stage TODO should we only handle 1 stage at a time?
            var selectedInput = unsignedRecoveryTransaction.Inputs[request.StageIndex];
            var selectedOutput = unsignedRecoveryTransaction.Outputs[request.StageIndex];
            
            unsignedRecoveryTransaction.Inputs.Clear();
            unsignedRecoveryTransaction.Outputs.Clear();
            unsignedRecoveryTransaction.Inputs.Add(selectedInput);
            unsignedRecoveryTransaction.Outputs.Add(selectedOutput);
            
            var changeAddress = accountInfo.GetNextChangeReceiveAddress();
            if (changeAddress == null)
                return Result.Failure("Could not get a change address");
            
            // add fee to the recovery trx
            var recoveryTransaction = walletOperations.AddFeeAndSignTransaction(changeAddress, unsignedRecoveryTransaction, words.Value.ToWalletWords(), accountInfo, selectedFeeEstimation.FeeRate);
            
            var result = await indexerService.PublishTransactionAsync(recoveryTransaction.Transaction.ToHex());
            
            return Result.Success();
        }
        
        private async Task<Result<SignatureInfo?>> LookupFounderSignatures(Guid walletId, Project project, DateTime createdAt, string eventId,
            Transaction investment, string projectPubKey)
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

            signService.LookupSignatureForInvestmentRequest(pubKey, projectPubKey, createdAt, eventId,
                async content =>
                {
                    var signatures =
                        await decrypter.DecryptNostrContentAsync(privateKeyHex, projectPubKey, content);

                    signatureInfo = serializer.Deserialize<SignatureInfo>(signatures);

                    var validSignatures =
                        investorTransactionActions.CheckInvestorRecoverySignatures(project.ToProjectInfo(),
                            investment, signatureInfo);
                    
                    //TODO do we need to store the signatures in the database at this point?
                    
                    tcs.SetResult(Result.Success(signatureInfo));
                },
                () => { if (!tcs.Task.IsCompleted) tcs.TrySetResult(result: Result.Success<SignatureInfo?>(null));});


            await tcs.Task;

            return tcs.Task.Result;
        }
    }
}


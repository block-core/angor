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
using MoreLinq;
using Angor.Sdk.Funding.Projects;

namespace Angor.Sdk.Funding.Investor.Operations;

public static class BuildRecoveryTransaction
{
    public record BuildRecoveryTransactionRequest(WalletId WalletId, ProjectId ProjectId, DomainFeerate SelectedFeeRate) : IRequest<Result<BuildRecoveryTransactionResponse>>;

    public record BuildRecoveryTransactionResponse(RecoveryTransactionDraft TransactionDraft);

    public class BuildRecoveryTransactionHandler(ISeedwordsProvider provider, IDerivationOperations derivationOperations,
            IProjectService projectService, IInvestorTransactionActions investorTransactionActions,
            IPortfolioService investmentService, INetworkConfiguration networkConfiguration,
            IWalletOperations walletOperations, ISignService signService,
            IEncryptionService decrypter, ISerializer serializer, ITransactionService transactionService,
            IWalletAccountBalanceService walletAccountBalanceService) : IRequestHandler<BuildRecoveryTransactionRequest, Result<BuildRecoveryTransactionResponse>>
    {
        public async Task<Result<BuildRecoveryTransactionResponse>> Handle(BuildRecoveryTransactionRequest request, CancellationToken cancellationToken)
        {
            var words = await provider.GetSensitiveData(request.WalletId.Value);
            if (words.IsFailure)
                return Result.Failure<BuildRecoveryTransactionResponse>(words.Error);

            // Get account info from database
            var accountBalanceResult = await walletAccountBalanceService.GetAccountBalanceInfoAsync(request.WalletId);
            if (accountBalanceResult.IsFailure)
                return Result.Failure<BuildRecoveryTransactionResponse>(accountBalanceResult.Error);

            var accountInfo = accountBalanceResult.Value.AccountInfo;

            var project = await projectService.GetAsync(request.ProjectId);
            if (project.IsFailure)
                return Result.Failure<BuildRecoveryTransactionResponse>(project.Error);
            var investments = await investmentService.GetByWalletId(request.WalletId.Value);
            if (investments.IsFailure)
                return Result.Failure<BuildRecoveryTransactionResponse>(investments.Error);

            var investment = investments.Value.ProjectIdentifiers.FirstOrDefault(p => p.ProjectIdentifier == request.ProjectId.Value);
            if (investment is null)
                return Result.Failure<BuildRecoveryTransactionResponse>("No investment found for this project");

            var investorPrivateKey = derivationOperations.DeriveInvestorPrivateKey(words.Value.ToWalletWords(), project.Value.FounderKey);

            var investmentTransaction = networkConfiguration.GetNetwork().CreateTransaction(investment.InvestmentTransactionHex);

            var signatureLookup = await LookupFounderSignatures(request.WalletId.Value, project.Value, investment.RequestEventTime.Value, investment.RequestEventId,
              investmentTransaction);

            if (signatureLookup.IsFailure)
                return Result.Failure<BuildRecoveryTransactionResponse>(signatureLookup.Error ?? "Could not retrieve founder signatures");
            if (signatureLookup.Value is null)
                return Result.Failure<BuildRecoveryTransactionResponse>("No founder signatures found");

            var unsignedRecoveryTransaction = investorTransactionActions.AddSignaturesToRecoverSeederFundsTransaction(project.Value.ToProjectInfo(), investmentTransaction, signatureLookup.Value, Encoders.Hex.EncodeData(investorPrivateKey.ToBytes()));

            var transactionInfo = await transactionService.GetTransactionInfoByIdAsync(investmentTransaction.GetHash().ToString());

            if (transactionInfo is null)
                return Result.Failure<BuildRecoveryTransactionResponse>("Could not find transaction info");

            transactionInfo.Outputs.ForEach((output, i) =>
             {
                 if (i < 2 || string.IsNullOrEmpty(output.SpentInTransaction))
                     return;

                 unsignedRecoveryTransaction.Inputs.RemoveAt(i - 2);
                 unsignedRecoveryTransaction.Outputs.RemoveAt(i - 2);
             });

            var changeAddress = accountInfo.GetNextChangeReceiveAddress();
            if (changeAddress == null)
                return Result.Failure<BuildRecoveryTransactionResponse>("Could not get a change address");

            // add fee to the recovery trx
            var recoveryTransaction = walletOperations.AddFeeAndSignTransaction(changeAddress, unsignedRecoveryTransaction, words.Value.ToWalletWords(), accountInfo, request.SelectedFeeRate.SatsPerKilobyte);

            return Result.Success(new BuildRecoveryTransactionResponse(new RecoveryTransactionDraft
            {
                SignedTxHex = recoveryTransaction.Transaction.ToHex(),
                TransactionFee = new Amount(recoveryTransaction.TransactionFee),
                TransactionId = recoveryTransaction.Transaction.GetHash().ToString()
            }));
        }

        private async Task<Result<SignatureInfo?>> LookupFounderSignatures(string walletId, Project project, DateTime createdAt, string eventId,
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
                () => { if (!tcs.Task.IsCompleted) tcs.TrySetResult(result: Result.Success<SignatureInfo?>(null)); });


            await tcs.Task;

            return tcs.Task.Result;
        }
    }
}

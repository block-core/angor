using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Funding.Shared.TransactionDrafts;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;
using Angor.Sdk.Funding.Projects;

namespace Angor.Sdk.Funding.Investor.Operations;

/// <summary>
/// Builds a transaction to release funds from penalty timelock.
/// This is the second step after recovery — once the penalty period has expired,
/// the investor can spend the penalty-locked outputs back to themselves.
/// Uses <see cref="IInvestorTransactionActions.BuildAndSignRecoverReleaseFundsTransaction"/>.
/// </summary>
public static class BuildPenaltyReleaseTransaction
{
    public record BuildPenaltyReleaseTransactionRequest(WalletId WalletId, ProjectId ProjectId, DomainFeerate SelectedFeeRate) : IRequest<Result<BuildPenaltyReleaseTransactionResponse>>;

    public record BuildPenaltyReleaseTransactionResponse(ReleaseTransactionDraft TransactionDraft);

    public class BuildPenaltyReleaseTransactionHandler(
        ISeedwordsProvider provider,
        IDerivationOperations derivationOperations,
        IProjectService projectService,
        IPortfolioService investmentService,
        INetworkConfiguration networkConfiguration,
        IInvestorTransactionActions investorTransactionActions,
        ITransactionService transactionService,
        IWalletAccountBalanceService walletAccountBalanceService
    ) : IRequestHandler<BuildPenaltyReleaseTransactionRequest, Result<BuildPenaltyReleaseTransactionResponse>>
    {
        public async Task<Result<BuildPenaltyReleaseTransactionResponse>> Handle(BuildPenaltyReleaseTransactionRequest request, CancellationToken cancellationToken)
        {
            var project = await projectService.GetAsync(request.ProjectId);
            if (project.IsFailure)
                return Result.Failure<BuildPenaltyReleaseTransactionResponse>(project.Error);

            var investments = await investmentService.GetByWalletId(request.WalletId.Value);
            if (investments.IsFailure)
                return Result.Failure<BuildPenaltyReleaseTransactionResponse>(investments.Error);

            var investment = investments.Value.ProjectIdentifiers
                .FirstOrDefault(p => p.ProjectIdentifier == request.ProjectId.Value);
            if (investment is null)
                return Result.Failure<BuildPenaltyReleaseTransactionResponse>("No investment found for this project");

            if (string.IsNullOrEmpty(investment.RecoveryTransactionId))
                return Result.Failure<BuildPenaltyReleaseTransactionResponse>("No recovery transaction found — recovery must be done before releasing from penalty");

            if (!string.IsNullOrEmpty(investment.RecoveryReleaseTransactionId))
                return Result.Failure<BuildPenaltyReleaseTransactionResponse>("Penalty release transaction has already been published");

            var words = await provider.GetSensitiveData(request.WalletId.Value);
            if (words.IsFailure)
                return Result.Failure<BuildPenaltyReleaseTransactionResponse>(words.Error);

            // Get account info from database
            var accountBalanceResult = await walletAccountBalanceService.GetAccountBalanceInfoAsync(request.WalletId);
            if (accountBalanceResult.IsFailure)
                return Result.Failure<BuildPenaltyReleaseTransactionResponse>(accountBalanceResult.Error);

            var accountInfo = accountBalanceResult.Value.AccountInfo;

            var changeAddress = accountInfo.GetNextChangeReceiveAddress();
            if (changeAddress == null)
                return Result.Failure<BuildPenaltyReleaseTransactionResponse>("Could not get a change address");

            var investorPrivateKey = derivationOperations.DeriveInvestorPrivateKey(words.Value.ToWalletWords(), project.Value.FounderKey);

            // Get the investment transaction
            var investmentTrxHex = investment.InvestmentTransactionHex;
            if (string.IsNullOrEmpty(investmentTrxHex))
                return Result.Failure<BuildPenaltyReleaseTransactionResponse>("Investment transaction hex not found");

            var investmentTransaction = networkConfiguration.GetNetwork().CreateTransaction(investmentTrxHex);

            // Get the recovery transaction from the indexer
            var recoveryTrxHex = await transactionService.GetTransactionHexByIdAsync(investment.RecoveryTransactionId);
            if (string.IsNullOrEmpty(recoveryTrxHex))
                return Result.Failure<BuildPenaltyReleaseTransactionResponse>("Recovery transaction not found on the network");

            var recoveryTransaction = networkConfiguration.GetNetwork().CreateTransaction(recoveryTrxHex);

            // Build and sign the penalty release transaction
            var feeEstimation = new FeeEstimation { FeeRate = request.SelectedFeeRate.SatsPerKilobyte / 1000 };

            var releaseTransactionInfo = investorTransactionActions.BuildAndSignRecoverReleaseFundsTransaction(
                project.Value.ToProjectInfo(),
                investmentTransaction,
                recoveryTransaction,
                changeAddress,
                feeEstimation,
                Encoders.Hex.EncodeData(investorPrivateKey.ToBytes()));

            return Result.Success(new BuildPenaltyReleaseTransactionResponse(new ReleaseTransactionDraft
            {
                SignedTxHex = releaseTransactionInfo.Transaction.ToHex(),
                TransactionFee = new Amount(releaseTransactionInfo.TransactionFee),
                TransactionId = releaseTransactionInfo.Transaction.GetHash().ToString()
            }));
        }
    }
}

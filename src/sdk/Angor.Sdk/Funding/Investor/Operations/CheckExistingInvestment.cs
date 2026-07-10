using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Funding.Investor.Operations;

/// <summary>
/// Checks whether a wallet has already invested in a specific project by querying the on-chain indexer.
/// If an investment is found but missing from the local portfolio, auto-recovers it.
/// </summary>
public static class CheckExistingInvestment
{
    public record CheckExistingInvestmentRequest(
        WalletId WalletId,
        ProjectId ProjectId,
        string FounderKey) : IRequest<Result<CheckExistingInvestmentResponse>>;

    public record CheckExistingInvestmentResponse(
        bool HasExistingInvestment,
        long InvestedAmountSats,
        string? TransactionId,
        bool WasRecovered);

    public class CheckExistingInvestmentHandler(
        ISeedwordsProvider seedwordsProvider,
        IDerivationOperations derivationOperations,
        IAngorIndexerService angorIndexerService,
        IPortfolioService portfolioService,
        ILogger<CheckExistingInvestmentHandler> logger
    ) : IRequestHandler<CheckExistingInvestmentRequest, Result<CheckExistingInvestmentResponse>>
    {
        public async Task<Result<CheckExistingInvestmentResponse>> Handle(
            CheckExistingInvestmentRequest request, CancellationToken cancellationToken)
        {
            // First check local portfolio (fast, no network needed)
            var portfolioResult = await portfolioService.GetByWalletId(request.WalletId.Value);
            if (portfolioResult.IsSuccess)
            {
                var localRecord = portfolioResult.Value?.ProjectIdentifiers
                    .FirstOrDefault(i => i.ProjectIdentifier == request.ProjectId.Value);

                if (localRecord != null)
                {
                    return Result.Success(new CheckExistingInvestmentResponse(
                        HasExistingInvestment: true,
                        InvestedAmountSats: localRecord.InvestedAmountSats,
                        TransactionId: localRecord.InvestmentTransactionHash,
                        WasRecovered: false));
                }
            }

            // Derive investor key and check the on-chain indexer
            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(request.WalletId.Value);
            if (sensitiveDataResult.IsFailure)
                return Result.Success(new CheckExistingInvestmentResponse(false, 0, null, false));

            var walletWords = sensitiveDataResult.Value.ToWalletWords();
            var investorKey = derivationOperations.DeriveInvestorKey(walletWords, request.FounderKey);

            var indexerResult = await Result.Try(() =>
                angorIndexerService.GetInvestmentAsync(request.ProjectId.Value, investorKey));

            if (indexerResult.IsFailure || indexerResult.Value == null)
                return Result.Success(new CheckExistingInvestmentResponse(false, 0, null, false));

            var onChain = indexerResult.Value;

            logger.LogInformation(
                "Found on-chain investment for project {ProjectId} (TxId: {TxId}, Amount: {Amount} sats) " +
                "that was missing from local portfolio. Recovering record.",
                request.ProjectId, onChain.TransactionId, onChain.TotalAmount);

            // Auto-recover: add the missing record to the portfolio
            await portfolioService.AddOrUpdate(request.WalletId.Value, new InvestmentRecord
            {
                ProjectIdentifier = request.ProjectId.Value,
                InvestmentTransactionHash = onChain.TransactionId,
                InvestorPubKey = investorKey,
                InvestedAmountSats = onChain.TotalAmount,
            });

            return Result.Success(new CheckExistingInvestmentResponse(
                HasExistingInvestment: true,
                InvestedAmountSats: onChain.TotalAmount,
                TransactionId: onChain.TransactionId,
                WasRecovered: true));
        }
    }
}

using Angor.Sdk.Common;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Projects.Infrastructure.Impl;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Funding.Shared.TransactionDrafts;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Blockcore.Consensus.TransactionInfo;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Funding.Investor.Operations;

public static class CreateInvestment
{
    public record CreateInvestmentTransactionRequest(
            WalletId WalletId,
            ProjectId ProjectId,
            Amount Amount,
            DomainFeerate FeeRate,
            byte? PatternIndex = null, // Required for Fund/Subscribe
            DateTime? InvestmentStartDate = null) // Required for Fund/Subscribe, defaults to now
        : IRequest<Result<CreateInvestmentTransactionResponse>>;

    public record CreateInvestmentTransactionResponse(InvestmentDraft InvestmentDraft);

    public class CreateInvestmentTransactionHandler(
            IProjectService projectService,
            IInvestorTransactionActions investorTransactionActions,
            ISeedwordsProvider seedwordsProvider,
            IWalletOperations walletOperations,
            IDerivationOperations derivationOperations,
            IWalletAccountBalanceService walletAccountBalanceService,
            ILogger<CreateInvestmentTransactionHandler> logger)
        : IRequestHandler<CreateInvestmentTransactionRequest, Result<CreateInvestmentTransactionResponse>>
    {
        public async Task<Result<CreateInvestmentTransactionResponse>> Handle(CreateInvestmentTransactionRequest transactionRequest, CancellationToken cancellationToken)
        {
            try
            {
                // Get the project
                var projectResult = await projectService.GetAsync(transactionRequest.ProjectId);
                if (projectResult.IsFailure)
                {
                    logger.LogWarning("Failed to get project {ProjectId}: {Error}", transactionRequest.ProjectId, projectResult.Error);
                    return Result.Failure<CreateInvestmentTransactionResponse>(projectResult.Error);
                }

                var project = projectResult.Value;

                // Get wallet words
                var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(transactionRequest.WalletId.Value);
                if (sensitiveDataResult.IsFailure)
                {
                    logger.LogWarning("Failed to get wallet data for {WalletId}: {Error}", transactionRequest.WalletId, sensitiveDataResult.Error);
                    return Result.Failure<CreateInvestmentTransactionResponse>(sensitiveDataResult.Error);
                }

                var walletWords = sensitiveDataResult.Value.ToWalletWords();

                // Derive investor key
                var investorKey = derivationOperations.DeriveInvestorKey(walletWords, project.FounderKey);

                // Convert Project to ProjectInfo
                var projectInfo = project.ToProjectInfo();

                // Create FundingParameters based on project type
                var fundingParametersResult = CreateFundingParameters(
                            projectInfo,
                            investorKey,
                            transactionRequest.Amount.Sats,
                            transactionRequest.PatternIndex,
                            transactionRequest.InvestmentStartDate);

                if (fundingParametersResult.IsFailure)
                {
                    logger.LogWarning("Failed to create funding parameters for project {ProjectId}: {Error}", transactionRequest.ProjectId, fundingParametersResult.Error);
                    return Result.Failure<CreateInvestmentTransactionResponse>(fundingParametersResult.Error);
                }

                var fundingParameters = fundingParametersResult.Value;

                // Validate funding parameters
                try
                {
                    fundingParameters.Validate(projectInfo);
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Funding parameters validation failed for project {ProjectId}: {Error}", transactionRequest.ProjectId, ex.Message);
                    return Result.Failure<CreateInvestmentTransactionResponse>($"Invalid funding parameters: {ex.Message}");
                }

                // Create investment transaction using FundingParameters
                var transactionResult = Result.Try(() => investorTransactionActions.CreateInvestmentTransaction(projectInfo, fundingParameters));

                if (transactionResult.IsFailure)
                {
                    logger.LogWarning("Failed to create investment transaction for project {ProjectId}: {Error}", transactionRequest.ProjectId, transactionResult.Error);
                    return Result.Failure<CreateInvestmentTransactionResponse>(transactionResult.Error);
                }

                // Sign the transaction
                var signedTxResult = await SignTransaction(
                        transactionRequest.WalletId,
                        walletWords,
                        transactionResult.Value,
                        transactionRequest.FeeRate.SatsPerKilobyte);

                if (signedTxResult.IsFailure)
                {
                    logger.LogWarning("Failed to sign transaction for project {ProjectId}: {Error}", transactionRequest.ProjectId, signedTxResult.Error);
                    return Result.Failure<CreateInvestmentTransactionResponse>(signedTxResult.Error);
                }

                // Calculate fees
                var signedTxHex = signedTxResult.Value.Transaction.ToHex();
                var minerFee = signedTxResult.Value.TransactionFee;
                var angorFee = signedTxResult.Value.Transaction.Outputs.AsIndexedOutputs().FirstOrDefault()?.TxOut.Value.Satoshi ?? 0;
                var trxId = signedTxResult.Value.Transaction.GetHash().ToString();

                logger.LogInformation("Investment transaction created successfully for project {ProjectId}, TxId: {TxId}, Amount: {Amount}", transactionRequest.ProjectId, trxId, transactionRequest.Amount.Sats);

                return Result.Success(new CreateInvestmentTransactionResponse(new InvestmentDraft(investorKey)
                {
                    TransactionFee = new Amount(minerFee + angorFee),
                    MinerFee = new Amount(minerFee),
                    AngorFee = new Amount(angorFee),
                    SignedTxHex = signedTxHex,
                    TransactionId = trxId,
                }));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating investment transaction for project {ProjectId}", transactionRequest.ProjectId);
                return Result.Failure<CreateInvestmentTransactionResponse>($"Error creating investment transaction: {ex.Message}");
            }
        }

        private Result<FundingParameters> CreateFundingParameters(
                    ProjectInfo projectInfo,
                    string investorKey,
                    long investmentAmount,
                    byte? patternIndex,
                    DateTime? investmentStartDate)
        {
            try
            {
                return projectInfo.ProjectType switch
                {
                    ProjectType.Invest => Result.Success(FundingParameters.CreateForInvest(projectInfo, investorKey, investmentAmount)),

                    ProjectType.Fund => CreateFundParameters(projectInfo, investorKey, investmentAmount, patternIndex, investmentStartDate),

                    ProjectType.Subscribe => CreateSubscribeParameters(projectInfo, investorKey, investmentAmount, patternIndex, investmentStartDate),

                    _ => Result.Failure<FundingParameters>($"Unknown project type: {projectInfo.ProjectType}")
                };
            }
            catch (Exception ex)
            {
                return Result.Failure<FundingParameters>($"Failed to create funding parameters: {ex.Message}");
            }
        }

        private Result<FundingParameters> CreateFundParameters(
                    ProjectInfo projectInfo,
                    string investorKey,
                    long investmentAmount,
                    byte? patternIndex,
                    DateTime? investmentStartDate)
        {
            if (!patternIndex.HasValue)
            {
                return Result.Failure<FundingParameters>("PatternIndex is required for Fund projects. Please select a funding pattern.");
            }

            if (projectInfo.DynamicStagePatterns == null || !projectInfo.DynamicStagePatterns.Any())
            {
                return Result.Failure<FundingParameters>("Project does not have any funding patterns configured.");
            }

            if (patternIndex.Value >= projectInfo.DynamicStagePatterns.Count)
            {
                return Result.Failure<FundingParameters>($"Invalid pattern index {patternIndex.Value}. Project has {projectInfo.DynamicStagePatterns.Count} patterns.");
            }

            var effectiveStartDate = investmentStartDate ?? DateTime.UtcNow;

            return Result.Success(FundingParameters.CreateForFund(
                                            projectInfo,
                                            investorKey,
                                            investmentAmount,
                                            patternIndex.Value,
                                            effectiveStartDate));
        }

        private Result<FundingParameters> CreateSubscribeParameters(
                    ProjectInfo projectInfo,
                    string investorKey,
                    long investmentAmount,
                    byte? patternIndex,
                    DateTime? investmentStartDate)
        {
            if (!patternIndex.HasValue)
            {
                return Result.Failure<FundingParameters>(
                "PatternIndex is required for Subscribe projects. Please select a subscription pattern.");
            }

            if (projectInfo.DynamicStagePatterns == null || !projectInfo.DynamicStagePatterns.Any())
            {
                return Result.Failure<FundingParameters>(
             "Project does not have any subscription patterns configured.");
            }

            if (patternIndex.Value >= projectInfo.DynamicStagePatterns.Count)
            {
                return Result.Failure<FundingParameters>(
                   $"Invalid pattern index {patternIndex.Value}. Project has {projectInfo.DynamicStagePatterns.Count} patterns.");
            }

            var effectiveStartDate = investmentStartDate ?? DateTime.UtcNow;

            return Result.Success(FundingParameters.CreateForSubscribe(
                                            projectInfo,
                                            investorKey,
                                            investmentAmount,
                                            patternIndex.Value,
                                            effectiveStartDate));
        }

        private async Task<Result<TransactionInfo>> SignTransaction(
                WalletId walletId,
                WalletWords walletWords,
                Transaction transaction,
                long feerate)
        {
            if (walletId == null) throw new ArgumentNullException(nameof(walletId));
            // Get account info from database
            var accountBalanceResult = await walletAccountBalanceService.GetAccountBalanceInfoAsync(walletId);
            if (accountBalanceResult.IsFailure)
            {
                return Result.Failure<TransactionInfo>(accountBalanceResult.Error);
            }

            var accountInfo = accountBalanceResult.Value.AccountInfo;

            var changeAddressResult = Result.Try(() => accountInfo.GetNextChangeReceiveAddress())
               .Ensure(s => !string.IsNullOrEmpty(s), "Change address cannot be empty");

            if (changeAddressResult.IsFailure)
            {
                return Result.Failure<TransactionInfo>(changeAddressResult.Error);
            }

            var changeAddress = changeAddressResult.Value!;

            var signedTransactionResult = Result.Try(() => 
                    walletOperations.AddInputsAndSignTransaction(changeAddress,
                                                                 transaction,
                                                                 walletWords,
                                                                 accountInfo,
                                                                 feerate));

            return signedTransactionResult;
        }
    }
}
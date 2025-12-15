using Angor.Sdk.Common;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Funding.Shared.TransactionDrafts;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.Consensus.TransactionInfo;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Funding.Investor.Operations;

public static class CreateInvestmentFromSpecificAddress
{
    public record CreateInvestmentFromSpecificAddressRequest(
            WalletId WalletId,
            ProjectId ProjectId,
            Amount Amount,
            DomainFeerate FeeRate,
            string FundingAddress,
            byte? PatternIndex = null,
            DateTime? InvestmentStartDate = null)
        : IRequest<Result<CreateInvestmentFromSpecificAddressResponse>>;

    public record CreateInvestmentFromSpecificAddressResponse(InvestmentDraft InvestmentDraft);

    public class CreateInvestmentFromSpecificAddressHandler(
            IProjectService projectService,
            IInvestorTransactionActions investorTransactionActions,
            ISeedwordsProvider seedwordsProvider,
            IWalletOperations walletOperations,
            IDerivationOperations derivationOperations,
            IWalletAccountBalanceService walletAccountBalanceService,
            IMempoolMonitoringService mempoolMonitoringService,
            ILogger<CreateInvestmentFromSpecificAddressHandler> logger)
        : IRequestHandler<CreateInvestmentFromSpecificAddressRequest, Result<CreateInvestmentFromSpecificAddressResponse>>
    {
        // Hardcoded timeout - will be moved to configuration later
        private readonly TimeSpan _monitoringTimeout = TimeSpan.FromMinutes(30);

        public async Task<Result<CreateInvestmentFromSpecificAddressResponse>> Handle(
            CreateInvestmentFromSpecificAddressRequest request, 
            CancellationToken cancellationToken)
        {
            try
            {
                // Step 1: Get the project
                var projectResult = await projectService.GetAsync(request.ProjectId);
                if (projectResult.IsFailure)
                {
                    logger.LogWarning("Failed to get project {ProjectId}: {Error}", request.ProjectId, projectResult.Error);
                    return Result.Failure<CreateInvestmentFromSpecificAddressResponse>(projectResult.Error);
                }

                var project = projectResult.Value;

                // Step 2: Get wallet words
                var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(request.WalletId.Value);
                if (sensitiveDataResult.IsFailure)
                {
                    logger.LogWarning("Failed to get wallet data for {WalletId}: {Error}", request.WalletId, sensitiveDataResult.Error);
                    return Result.Failure<CreateInvestmentFromSpecificAddressResponse>(sensitiveDataResult.Error);
                }

                var walletWords = sensitiveDataResult.Value.ToWalletWords();

                // Step 3: Derive investor key
                var investorKey = derivationOperations.DeriveInvestorKey(walletWords, project.FounderKey);

                // Step 4: Convert Project to ProjectInfo
                var projectInfo = project.ToProjectInfo();

                // Step 5: Create FundingParameters based on project type
                var fundingParametersResult = CreateFundingParameters(
                    projectInfo,
                    investorKey,
                    request.Amount.Sats,
                    request.PatternIndex,
                    request.InvestmentStartDate);

                if (fundingParametersResult.IsFailure)
                {
                    logger.LogWarning("Failed to create funding parameters for project {ProjectId}: {Error}", 
                        request.ProjectId, fundingParametersResult.Error);
                    return Result.Failure<CreateInvestmentFromSpecificAddressResponse>(fundingParametersResult.Error);
                }

                var fundingParameters = fundingParametersResult.Value;

                // Step 6: Validate funding parameters
                try
                {
                    fundingParameters.Validate(projectInfo);
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Funding parameters validation failed for project {ProjectId}: {Error}", 
                        request.ProjectId, ex.Message);
                    return Result.Failure<CreateInvestmentFromSpecificAddressResponse>($"Invalid funding parameters: {ex.Message}");
                }

                // Step 7: Create investment transaction template (unsigned)
                var transactionResult = Result.Try(() => 
                    investorTransactionActions.CreateInvestmentTransaction(projectInfo, fundingParameters));

                if (transactionResult.IsFailure)
                {
                    logger.LogWarning("Failed to create investment transaction for project {ProjectId}: {Error}", 
                        request.ProjectId, transactionResult.Error);
                    return Result.Failure<CreateInvestmentFromSpecificAddressResponse>(transactionResult.Error);
                }

                var investmentTransaction = transactionResult.Value;

                // Step 8: Calculate required amount (investment + fees estimate)
                var estimatedFee = request.FeeRate.SatsPerKilobyte * investmentTransaction.GetVirtualSize(4) / 1000;
                var requiredAmount = request.Amount.Sats + estimatedFee + (estimatedFee / 2); // Add 50% buffer for fee calculation

                logger.LogInformation("Monitoring address {FundingAddress} for {RequiredAmount} sats (investment: {InvestmentAmount}, estimated fees: {EstimatedFee})",
                    request.FundingAddress, requiredAmount, request.Amount.Sats, estimatedFee);

                // Step 9: Monitor mempool for funding address
                List<UtxoData> detectedUtxos;
                try
                {
                    detectedUtxos = await mempoolMonitoringService.MonitorAddressForFundsAsync(
                        request.FundingAddress,
                        requiredAmount,
                        _monitoringTimeout,
                        cancellationToken);
                }
                catch (TimeoutException ex)
                {
                    logger.LogWarning("Mempool monitoring timeout for address {FundingAddress}: {Error}", 
                        request.FundingAddress, ex.Message);
                    return Result.Failure<CreateInvestmentFromSpecificAddressResponse>(
                        $"Timeout waiting for funds on address {request.FundingAddress}. Please ensure funds are sent to this address.");
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("Investment creation cancelled for project {ProjectId}", request.ProjectId);
                    return Result.Failure<CreateInvestmentFromSpecificAddressResponse>("Investment creation was cancelled");
                }

                logger.LogInformation("Detected {Count} UTXO(s) totaling {TotalAmount} sats on address {FundingAddress}", 
                    detectedUtxos.Count, detectedUtxos.Sum(u => u.value), request.FundingAddress);

                // Step 10: Update account info with detected UTXOs and reserve them
                var updateAccountResult = await UpdateAccountWithDetectedUtxos(
                    request.WalletId,
                    request.FundingAddress,
                    detectedUtxos);

                if (updateAccountResult.IsFailure)
                {
                    return Result.Failure<CreateInvestmentFromSpecificAddressResponse>(updateAccountResult.Error);
                }

                var accountInfo = updateAccountResult.Value;

                // Step 11: Sign the transaction using the specific funding address
                var signedTxResult = await SignTransactionFromAddress(
                    request.FundingAddress,
                    walletWords,
                    investmentTransaction,
                    accountInfo,
                    request.FeeRate.SatsPerKilobyte);

                if (signedTxResult.IsFailure)
                {
                    logger.LogWarning("Failed to sign transaction for project {ProjectId}: {Error}", 
                        request.ProjectId, signedTxResult.Error);
                    return Result.Failure<CreateInvestmentFromSpecificAddressResponse>(signedTxResult.Error);
                }

                // Step 12: Calculate fees
                var signedTxHex = signedTxResult.Value.Transaction.ToHex();
                var minerFee = signedTxResult.Value.TransactionFee;
                var angorFee = signedTxResult.Value.Transaction.Outputs.AsIndexedOutputs().FirstOrDefault()?.TxOut.Value.Satoshi ?? 0;
                var trxId = signedTxResult.Value.Transaction.GetHash().ToString();

                logger.LogInformation("Investment transaction created successfully from address {FundingAddress} for project {ProjectId}, TxId: {TxId}, Amount: {Amount}", 
                    request.FundingAddress, request.ProjectId, trxId, request.Amount.Sats);

                return Result.Success(new CreateInvestmentFromSpecificAddressResponse(new InvestmentDraft(investorKey)
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
                logger.LogError(ex, "Error creating investment transaction from specific address for project {ProjectId}", 
                    request.ProjectId);
                return Result.Failure<CreateInvestmentFromSpecificAddressResponse>(
                    $"Error creating investment transaction: {ex.Message}");
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
                    ProjectType.Invest => Result.Success(
                        FundingParameters.CreateForInvest(projectInfo, investorKey, investmentAmount)),

                    ProjectType.Fund => CreateFundParameters(
                        projectInfo, investorKey, investmentAmount, patternIndex, investmentStartDate),

                    ProjectType.Subscribe => CreateSubscribeParameters(
                        projectInfo, investorKey, investmentAmount, patternIndex, investmentStartDate),

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
                return Result.Failure<FundingParameters>(
                    "PatternIndex is required for Fund projects. Please select a funding pattern.");
            }

            if (projectInfo.DynamicStagePatterns == null || !projectInfo.DynamicStagePatterns.Any())
            {
                return Result.Failure<FundingParameters>(
                    "Project does not have any funding patterns configured.");
            }

            if (patternIndex.Value >= projectInfo.DynamicStagePatterns.Count)
            {
                return Result.Failure<FundingParameters>(
                    $"Invalid pattern index {patternIndex.Value}. Project has {projectInfo.DynamicStagePatterns.Count} patterns.");
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

        private async Task<Result<AccountInfo>> UpdateAccountWithDetectedUtxos(
            WalletId walletId,
            string fundingAddress,
            List<UtxoData> detectedUtxos)
        {
            try
            {
                // Get account info from database
                var accountBalanceResult = await walletAccountBalanceService.GetAccountBalanceInfoAsync(walletId);
                if (accountBalanceResult.IsFailure)
                {
                    return Result.Failure<AccountInfo>(accountBalanceResult.Error);
                }

                var accountInfo = accountBalanceResult.Value.AccountInfo;

                // Find the address info for the funding address
                var addressInfo = accountInfo.AllAddresses()
                    .FirstOrDefault(a => a.Address == fundingAddress);

                if (addressInfo == null)
                {
                    return Result.Failure<AccountInfo>(
                        $"Funding address {fundingAddress} not found in account. Please ensure the address belongs to this wallet.");
                }

                // Add detected UTXOs to the address if not already present
                foreach (var utxo in detectedUtxos)
                {
                    if (!addressInfo.UtxoData.Any(u => u.outpoint.ToString() == utxo.outpoint.ToString()))
                    {
                        addressInfo.UtxoData.Add(utxo);
                        logger.LogInformation("Added UTXO {Outpoint} with value {Value} sats to address {Address}", 
                            utxo.outpoint, utxo.value, fundingAddress);
                    }

                    // Reserve the UTXO for this investment
                    if (!accountInfo.UtxoReservedForInvestment.Contains(utxo.outpoint.ToString()))
                    {
                        accountInfo.UtxoReservedForInvestment.Add(utxo.outpoint.ToString());
                        logger.LogInformation("Reserved UTXO {Outpoint} for investment", utxo.outpoint);
                    }
                }

                return Result.Success(accountInfo);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating account with detected UTXOs for address {FundingAddress}", fundingAddress);
                return Result.Failure<AccountInfo>($"Failed to update account: {ex.Message}");
            }
        }

        private Task<Result<TransactionInfo>> SignTransactionFromAddress(
            string fundingAddress,
            WalletWords walletWords,
            Transaction transaction,
            AccountInfo accountInfo,
            long feerate)
        {
            try
            {
                var changeAddressResult = Result.Try(() => accountInfo.GetNextChangeReceiveAddress())
                    .Ensure(s => !string.IsNullOrEmpty(s), "Change address cannot be empty");

                if (changeAddressResult.IsFailure)
                {
                    return Task.FromResult(Result.Failure<TransactionInfo>(changeAddressResult.Error));
                }

                var changeAddress = changeAddressResult.Value!;

                var signedTransactionResult = Result.Try(() =>
                    walletOperations.AddInputsFromAddressAndSignTransaction(
                        fundingAddress,
                        changeAddress,
                        transaction,
                        walletWords,
                        accountInfo,
                        feerate));

                return Task.FromResult(signedTransactionResult);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error signing transaction from address {FundingAddress}", fundingAddress);
                return Task.FromResult(Result.Failure<TransactionInfo>($"Failed to sign transaction: {ex.Message}"));
            }
        }
    }
}


using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Angor.Contexts.Funding.Founder.Operations;

internal static class CreateProjectConstants
{
    public static class CreateProject
    {
        /// <summary>
        /// Creates the blockchain transaction for a project.
        /// Prerequisites: Nostr profile and project info must be created first.
        /// </summary>
        public record CreateProjectRequest(
                WalletId WalletId,
                long SelectedFeeRate,
                CreateProjectDto Project,
                string ProjectInfoEventId,
                ProjectSeedDto ProjectSeedDto) // Event ID from CreateProjectInfo
                : IRequest<Result<TransactionDraft>>;

        public class CreateProjectHandler(
                ISeedwordsProvider seedwordsProvider,
                IDerivationOperations derivationOperations,
                IFounderTransactionActions founderTransactionActions,
                IWalletOperations walletOperations,
                IWalletAccountBalanceService walletAccountBalanceService,
                ILogger<CreateProjectHandler> logger
                ) : IRequestHandler<CreateProjectRequest, Result<TransactionDraft>>
        {
            public async Task<Result<TransactionDraft>> Handle(CreateProjectRequest request, CancellationToken cancellationToken)
            {
                if (request.ProjectSeedDto == null)
                {
                    logger.LogDebug("FounderKeys is null in CreateProjectRequest for WalletId {WalletId}.", request.WalletId);
                    return Result.Failure<TransactionDraft>("FounderKeys cannot be null.");
                }

                var wallet = await seedwordsProvider.GetSensitiveData(request.WalletId.Value);

                ProjectSeedDto? newProjectKeys = request.ProjectSeedDto;

                var transactionInfo = await CreateProjectTransaction(
                      request.WalletId.Value,
                      wallet.Value.ToWalletWords(),
                      request.SelectedFeeRate,
                      newProjectKeys.FounderKey,
                      newProjectKeys.ProjectIdentifier,
                      request.ProjectInfoEventId);

                if (transactionInfo.IsFailure)
                {
                    logger.LogDebug("Failed to create project transaction for Project {ProjectName} (WalletId: {WalletId}): {Error}", request.Project.ProjectName, request.WalletId.Value, transactionInfo.Error);
                    return Result.Failure<TransactionDraft>(transactionInfo.Error);
                }

                // Return the transaction draft without publishing
                // Publishing will be handled by FounderAppService.SubmitTransactionFromDraft
                return Result.Success(new TransactionDraft
                {
                    SignedTxHex = transactionInfo.Value.Transaction.ToHex(),
                    TransactionId = transactionInfo.Value.Transaction.GetHash().ToString(),
                    TransactionFee = new Amount(transactionInfo.Value.TransactionFee)
                });
            }

            private async Task<Result<TransactionInfo>> CreateProjectTransaction(
                    string walletId,
                    WalletWords words,
                    long selectedFee,
                    string founderKey,
                    string projectIdentifier,
                    string projectInfoEventId)
            {
                var accountBalanceInfo = await RefreshWalletBalance(walletId);
                if (accountBalanceInfo.IsFailure)
                {
                    logger.LogDebug("Failed to get account balance information for WalletId {WalletId}: {Error}",
                     walletId, accountBalanceInfo.Error);
                    throw new InvalidOperationException("Failed to get account balance information");
                }

                var accountInfo = accountBalanceInfo.Value.AccountInfo;

                var unsignedTransaction = founderTransactionActions.CreateNewProjectTransaction(
                        founderKey,
                        derivationOperations.AngorKeyToScript(projectIdentifier),
                        NetworkConfiguration.AngorCreateFeeSats,
                        NetworkConfiguration.NostrEventIdKeyType,
                        projectInfoEventId);

                var signedTransaction = walletOperations.AddInputsAndSignTransaction(
                        accountInfo.GetNextChangeReceiveAddress()!,
                        unsignedTransaction,
                        words,
                        accountInfo,
                        selectedFee);

                return Result.Success(signedTransaction);
            }

            private async Task<Result<AccountBalanceInfo>> RefreshWalletBalance(string walletId)
            {
                try
                {
                    // Try to get from service first
                    var dbResult = await walletAccountBalanceService.GetAccountBalanceInfoAsync(walletId);

                    if (dbResult.IsFailure)
                    {
                        logger.LogDebug("Failed to get account balance info from DB for WalletId {WalletId}: {Error}",
                     walletId, dbResult.Error);
                        return Result.Failure<AccountBalanceInfo>(dbResult.Error);
                    }

                    // Refresh the existing balance
                    var refreshResult = await walletAccountBalanceService.RefreshAccountBalanceInfoAsync(walletId);

                    if (refreshResult.IsFailure)
                    {
                        logger.LogDebug("Failed to refresh account balance info for WalletId {WalletId}: {Error}",
                       walletId, refreshResult.Error);
                        return Result.Failure<AccountBalanceInfo>(refreshResult.Error);
                    }

                    return Result.Success(refreshResult.Value);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error refreshing balance for wallet {WalletId}", walletId);
                    return Result.Failure<AccountBalanceInfo>($"Error refreshing balance: {e.Message}");
                }
            }
        }
    }
}
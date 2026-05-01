using Angor.Data.Documents.Interfaces;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Domain;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Integration.Lightning.Models;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Funding.Services;

public class DatabaseManagementService(
    IGenericDocumentCollection<Project> projects,
    IGenericDocumentCollection<DerivedProjectKeys> derivedProjectKeys,
    IGenericDocumentCollection<BoltzSwapDocument> boltzSwaps,
    IGenericDocumentCollection<WalletAccountBalanceInfo> walletAccountBalance,
    IGenericDocumentCollection<QueryTransaction> queryTransactions,
    IGenericDocumentCollection<TransactionHexDocument> transactionHexDocuments,
    IGenericDocumentCollection<InvestmentRecordsDocument> investmentRecords,
    IGenericDocumentCollection<InvestmentHandshake> investmentHandshakes,
    IGenericDocumentCollection<FounderProjectsDocument> founderProjects,
    ILogger<DatabaseManagementService> logger) : IDatabaseManagementService
{
    public async Task<Result> DeleteAllDataAsync()
    {
        logger.LogInformation("Deleting all document collections");

        var results = new List<(string Name, Result<int> Result)>
        {
            ("Project", await projects.DeleteAllAsync()),
            ("DerivedProjectKeys", await derivedProjectKeys.DeleteAllAsync()),
            ("BoltzSwapDocument", await boltzSwaps.DeleteAllAsync()),
            ("WalletAccountBalanceInfo", await walletAccountBalance.DeleteAllAsync()),
            ("QueryTransaction", await queryTransactions.DeleteAllAsync()),
            ("TransactionHexDocument", await transactionHexDocuments.DeleteAllAsync()),
            ("InvestmentRecordsDocument", await investmentRecords.DeleteAllAsync()),
            ("InvestmentHandshake", await investmentHandshakes.DeleteAllAsync()),
            ("FounderProjectsDocument", await founderProjects.DeleteAllAsync()),
        };

        var failures = results.Where(r => r.Result.IsFailure).ToList();

        if (failures.Count > 0)
        {
            var errors = string.Join("; ", failures.Select(f => $"{f.Name}: {f.Result.Error}"));
            logger.LogError("Failed to delete some collections: {Errors}", errors);
            return Result.Failure($"Failed to delete some collections: {errors}");
        }

        var totalDeleted = results.Sum(r => r.Result.Value);
        logger.LogInformation("Successfully deleted {TotalDeleted} documents across all collections", totalDeleted);

        return Result.Success();
    }
}

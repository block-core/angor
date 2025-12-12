using Angor.Sdk.Funding.Projects.Domain;
using Angor.Shared.Models;
using Blockcore.Consensus.TransactionInfo;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Funding.Projects.Infrastructure.Interfaces;

public interface IProjectInvestmentsService
{
    Task<Result<IEnumerable<StageData>>> ScanFullInvestments(string projectId);

    Task<Result<StageDataTrx>> CheckSpentFund(QueryTransactionOutput output, Transaction investmentTransaction,
        ProjectInfo projectInfo,
        int stageIndex);

    Task<Result<InvestmentSpendingLookup>> ScanInvestmentSpends(ProjectInfo project, string transactionId);
}
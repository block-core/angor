using Angor.Contexts.Funding.Projects.Domain;
using Angor.Shared.Models;
using Blockcore.Consensus.TransactionInfo;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;

public interface IProjectInvestmentsRepository
{
    Task<Result<IEnumerable<StageData>>> ScanFullInvestments(string projectId);

    Task<Result<StageDataTrx>> CheckSpentFund(QueryTransactionOutput output, Transaction investmentTransaction,
        ProjectInfo projectInfo,
        int stageIndex);
}
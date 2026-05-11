using Angor.Shared.Models;
using NBitcoin;
using NBitcoin;

namespace Angor.Shared.Protocol.TransactionBuilders;

public interface IInvestmentTransactionBuilder
{
    Transaction BuildInvestmentTransaction(ProjectInfo projectInfo, Script opReturnScript,
        IEnumerable<ProjectScripts> projectScripts, long totalInvestmentAmount);

    Transaction BuildUpfrontRecoverFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, int penaltyDays,
        string investorKey);

    Transaction BuildUpfrontUnfundedReleaseFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, 
        string investorReleaseKey);
}
using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;

namespace Angor.Shared.ProtocolNew.TransactionBuilders;

public interface IInvestmentTransactionBuilder
{
    Transaction BuildInvestmentTransaction(ProjectInfo projectInfo, Script opReturnScript,
        IEnumerable<ProjectScripts> projectScripts, long totalInvestmentAmount);

    Transaction BuildUpfrontRecoverFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, int penaltyDays,
        string investorKey);

    Transaction BuildUpfrontUnfundedReleaseFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, 
        string investorReleaseKey);
}
using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;

namespace Angor.Shared.ProtocolNew.TransactionBuilders;

public interface IInvestmentTransactionBuilder
{
    Transaction BuildInvestmentTransaction(ProjectInfo projectInfo, Script opReturnScript,
        IEnumerable<ProjectScripts> projectScripts, long totalInvestmentAmount);

    IEnumerable<Transaction> BuildUpfrontRecoverFundsTransactions(Transaction investmentTransaction, DateTime penaltyDate,
        string investorReceiveAddress);
    
    Transaction BuildUpfrontRecoverFundsTransaction(Transaction investmentTransaction, DateTime penaltyDate,
        string investorReceiveAddress);
}
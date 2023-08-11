using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;

namespace Angor.Shared.ProtocolNew;

public interface IFounderTransactionActions
{
    List<string> FounderSignInvestorRecoveryTransactions(ProjectInfo projectInfo, string investmentTrxHex, 
        IEnumerable<Transaction> transactions, string founderPrivateKey);

    Transaction SpendFounderStage(ProjectInfo projectInfo, IEnumerable<string> investmentTransactionsHex,
        int stageNumber, Script founderRecieveAddress, string founderPrivateKey,
        FeeEstimation fee);
}
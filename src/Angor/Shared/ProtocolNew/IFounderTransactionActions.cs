using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;

namespace Angor.Shared.ProtocolNew;

public interface IFounderTransactionActions
{
    List<string> SignInvestorRecoveryTransactions(ProjectInfo projectInfo, string investmentTrxHex, 
        Transaction recoveryTransaction, string founderPrivateKey);

    Transaction SpendFounderStage(ProjectInfo projectInfo, IEnumerable<string> investmentTransactionsHex,
        int stageNumber, Script founderRecieveAddress, string founderPrivateKey,
        FeeEstimation fee);
}
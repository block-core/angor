using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;

namespace Angor.Shared.ProtocolNew;

public interface IFounderTransactionActions
{
    SignatureInfo SignInvestorRecoveryTransactions(ProjectInfo projectInfo, string investmentTrxHex, 
        Transaction recoveryTransaction, string founderPrivateKey);

    TransactionInfo SpendFounderStage(ProjectInfo projectInfo, IEnumerable<string> investmentTransactionsHex,
        int stageNumber, Script founderRecieveAddress, string founderPrivateKey,
        FeeEstimation fee);

    Transaction CreateNewProjectTransaction(string founderKey, Script angorKey, long angorFeeSatoshis, string nostrPubKey);
}
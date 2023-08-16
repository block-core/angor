using Angor.Shared.Models;
using Blockcore.Consensus.TransactionInfo;

namespace Angor.Shared.ProtocolNew;

public interface ISeederTransactionActions
{
    Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, string investorKey,
        string investorSecretHash, long totalInvestmentAmount);

    IEnumerable<Transaction> BuildRecoverSeederFundsTransactions(Transaction investmentTransaction, DateTime penaltyDate, string investorReceiveAddress);

    Transaction RecoverEndOfProjectFunds(string transactionHex, ProjectInfo projectInfo, int stageIndex, string investorReceiveAddress,
        string investorPrivateKey, FeeEstimation feeEstimation);
}
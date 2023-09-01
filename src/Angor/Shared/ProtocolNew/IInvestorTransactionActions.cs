using Angor.Shared.Models;
using Blockcore.Consensus.TransactionInfo;

namespace Angor.Shared.ProtocolNew;

public interface IInvestorTransactionActions
{
    Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, string investorKey, long totalInvestmentAmount);
    IEnumerable<Transaction> BuildRecoverInvestorFundsTransactions(Transaction investmentTransaction, DateTime penaltyDate, string investorReceiveAddress);
    Transaction RecoverEndOfProjectFunds(string transactionHex, ProjectInfo projectInfo, int stageIndex, string investorReceiveAddress, string investorPrivateKey, FeeEstimation feeEstimation);
    
    Transaction RecoverRemainingFundsWithPenalty(string transactionHex, ProjectInfo projectInfo, int stageIndex, string investorReceiveAddress, string investorPrivateKey, FeeEstimation feeEstimation, IEnumerable<byte[]> seederSecrets);
}
using Angor.Shared.Models;
using Blockcore.Consensus.TransactionInfo;

namespace Angor.Shared.ProtocolNew;

public interface IInvestorTransactionActions
{
    Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, string investorKey, long totalInvestmentAmount);
    Transaction BuildRecoverInvestorFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction);
    Transaction RecoverEndOfProjectFunds(string transactionHex, ProjectInfo projectInfo, int stageIndex, string investorReceiveAddress, string investorPrivateKey, FeeEstimation feeEstimation);
    
    Transaction RecoverRemainingFundsWithOutPenalty(string transactionHex, ProjectInfo projectInfo, int stageIndex, string investorReceiveAddress, string investorPrivateKey, FeeEstimation feeEstimation, IEnumerable<byte[]> seederSecrets);

    Transaction AddSignaturesToRecoverSeederFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, SignatureInfo founderSignatures, string privateKey);

    bool CheckInvestorRecoverySignatures(ProjectInfo projectInfo, Transaction investmentTransaction, SignatureInfo founderSignatures);

}
using Angor.Shared.Models;
using Transaction = Blockcore.Consensus.TransactionInfo.Transaction;

namespace Angor.Shared.Protocol;

public interface IInvestorTransactionActions
{
    Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, string investorKey, long totalInvestmentAmount);

    Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, FundingParameters parameters);

    ProjectScriptType DiscoverUsedScript(ProjectInfo projectInfo, Transaction investmentTransaction, int stageIndex, string witScript);

    Transaction BuildRecoverInvestorFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction);

    Transaction BuildUnfundedReleaseInvestorFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, string investorReleaseKey);

    TransactionInfo BuildAndSignRecoverReleaseFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, Transaction recoveryTransaction, string investorReceiveAddress, FeeEstimation feeEstimation, string investorPrivateKey);

    TransactionInfo RecoverEndOfProjectFunds(string transactionHex, ProjectInfo projectInfo, int startStageNumber, string investorReceiveAddress, string investorPrivateKey, FeeEstimation feeEstimation);

    TransactionInfo RecoverRemainingFundsWithOutPenalty(string transactionHex, ProjectInfo projectInfo, int startStageNumber, string investorReceiveAddress, string investorPrivateKey, FeeEstimation feeEstimation, IEnumerable<byte[]> seederSecrets);

    Transaction AddSignaturesToRecoverSeederFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, SignatureInfo founderSignatures, string investorPrivateKey);

    Transaction AddSignaturesToUnfundedReleaseFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, SignatureInfo founderSignatures, string investorPrivateKey, string investorReleaseKey);

    bool CheckInvestorRecoverySignatures(ProjectInfo projectInfo, Transaction investmentTransaction, SignatureInfo founderSignatures);

    bool CheckInvestorUnfundedReleaseSignatures(ProjectInfo projectInfo, Transaction investmentTransaction, SignatureInfo founderSignatures, string investorReleaseKey);

    bool IsInvestmentAbovePenaltyThreshold(ProjectInfo projectInfo, long investmentAmount);
}
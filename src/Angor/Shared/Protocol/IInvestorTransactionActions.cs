using Angor.Shared.Models;
using Blockcore.Consensus.TransactionInfo;

namespace Angor.Shared.Protocol;

public interface IInvestorTransactionActions
{
    /// <summary>
    /// Creates an investment transaction using the legacy method signature (backward compatible).
    /// </summary>
    Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, string investorKey, long totalInvestmentAmount);
    
    /// <summary>
    /// Creates an investment transaction using project parameters.
    /// Recommended for new code as it supports pattern selection and explicit start dates.
    /// </summary>
    Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, ProjectParameters parameters);
    
    Transaction BuildRecoverInvestorFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction);
    Transaction BuildUnfundedReleaseInvestorFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, string investorReleaseKey);

    TransactionInfo RecoverEndOfProjectFunds(string transactionHex, ProjectInfo projectInfo, int stageIndex, string investorReceiveAddress, string investorPrivateKey, FeeEstimation feeEstimation);
    
    TransactionInfo RecoverRemainingFundsWithOutPenalty(string transactionHex, ProjectInfo projectInfo, int stageIndex, string investorReceiveAddress, string investorPrivateKey, FeeEstimation feeEstimation, IEnumerable<byte[]> seederSecrets);
    TransactionInfo BuildAndSignRecoverReleaseFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, Transaction recoveryTransaction, string investorReceiveAddress, FeeEstimation feeEstimation, string investorPrivateKey);

    Transaction AddSignaturesToRecoverSeederFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, SignatureInfo founderSignatures, string investorPrivateKey);
    Transaction AddSignaturesToUnfundedReleaseFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, SignatureInfo founderSignatures, string investorPrivateKey, string investorReleaseKey);

    bool CheckInvestorRecoverySignatures(ProjectInfo projectInfo, Transaction investmentTransaction, SignatureInfo founderSignatures);
    bool CheckInvestorUnfundedReleaseSignatures(ProjectInfo projectInfo, Transaction investmentTransaction, SignatureInfo founderSignatures, string investorReleaseKey);
    ProjectScriptType DiscoverUsedScript(ProjectInfo projectInfo, Transaction investmentTransaction, int stageIndex, string witScript);
    
    bool IsInvestmentAbovePenaltyThreshold(ProjectInfo projectInfo, Transaction investmentTransaction);
    bool IsInvestmentAbovePenaltyThreshold(ProjectInfo projectInfo, long investmentAmount);
}
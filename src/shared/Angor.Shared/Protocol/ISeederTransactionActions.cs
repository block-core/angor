using Angor.Shared.Models;
using NBitcoin;

namespace Angor.Shared.Protocol;

public interface ISeederTransactionActions
{
    Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, FundingParameters parameters);

    [Obsolete("Use CreateInvestmentTransaction(ProjectInfo projectInfo, FundingParameters parameters) instead")]
    Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, string investorKey, uint256 investorSecretHash, long totalInvestmentAmount);
    Transaction BuildRecoverSeederFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, int penaltyDays, string investorKey);
    TransactionInfo RecoverEndOfProjectFunds(string investmentTransactionHex, ProjectInfo projectInfo, int stageIndex, string investorReceiveAddress, AngorKey investorPrivateKey, FeeEstimation feeEstimation);

    Transaction AddSignaturesToRecoverSeederFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction,
        string receiveAddress, SignatureInfo founderSignatures, AngorKey privateKey, string? secret);
}
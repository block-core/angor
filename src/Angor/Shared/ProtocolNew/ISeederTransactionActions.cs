using Angor.Shared.Models;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;

namespace Angor.Shared.ProtocolNew;

public interface ISeederTransactionActions
{
    Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, string investorKey, uint256 investorSecretHash, long totalInvestmentAmount);
    Transaction BuildRecoverSeederFundsTransaction(Transaction investmentTransaction, DateTime penaltyDate, string investorKey);
    Transaction RecoverEndOfProjectFunds(string investmentTransactionHex, ProjectInfo projectInfo, int stageIndex, string investorReceiveAddress, string investorPrivateKey, FeeEstimation feeEstimation);

    Transaction AddSignaturesToRecoverSeederFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction,
        string receiveAddress, List<string> founderSignatures, string privateKey, string? secret);
}
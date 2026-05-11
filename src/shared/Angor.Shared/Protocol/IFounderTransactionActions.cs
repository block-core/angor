using Angor.Shared.Models;
using NBitcoin;
using NBitcoin;

namespace Angor.Shared.Protocol;

public interface IFounderTransactionActions
{
    SignatureInfo SignInvestorRecoveryTransactions(ProjectInfo projectInfo, string investmentTrxHex, Transaction recoveryTransaction, string founderPrivateKey);

    TransactionInfo SpendFounderStage(ProjectInfo projectInfo, IEnumerable<string> investmentTransactionsHex, int stageNumber, Script founderRecieveAddress, string founderPrivateKey, FeeEstimation fee);

    TransactionInfo SpendFounderStage(ProjectInfo projectInfo, IEnumerable<StageTransactionInput> stageTransactionInput, Script founderRecieveAddress, string founderPrivateKey, FeeEstimation fee);

    Transaction CreateNewProjectTransaction(string founderKey, Script angorKey, long angorFeeSatoshis, short keyType, string nostrEventId);
}
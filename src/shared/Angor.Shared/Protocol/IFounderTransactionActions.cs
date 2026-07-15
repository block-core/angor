using Angor.Shared.Models;
using NBitcoin;

namespace Angor.Shared.Protocol;

public interface IFounderTransactionActions
{
    SignatureInfo SignInvestorRecoveryTransactions(ProjectInfo projectInfo, string investmentTrxHex, Transaction recoveryTransaction, AngorKey founderPrivateKey);

    TransactionInfo SpendFounderStage(ProjectInfo projectInfo, IEnumerable<string> investmentTransactionsHex, int stageNumber, Script founderRecieveAddress, AngorKey founderPrivateKey, FeeEstimation fee);

    TransactionInfo SpendFounderStage(ProjectInfo projectInfo, IEnumerable<StageTransactionInput> stageTransactionInput, Script founderRecieveAddress, AngorKey founderPrivateKey, FeeEstimation fee);

    Transaction CreateNewProjectTransaction(string founderKey, Script angorKey, long angorFeeSatoshis, short keyType, string nostrEventId);
}
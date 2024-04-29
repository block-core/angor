using Angor.Shared.Models;
using NBitcoin;
using Transaction = Blockcore.Consensus.TransactionInfo.Transaction;

namespace Angor.Shared.ProtocolNew.TransactionBuilders;

public interface ISpendingTransactionBuilder
{
    TransactionInfo BuildRecoverInvestorRemainingFundsInProject(string investmentTransactionHex, ProjectInfo projectInfo, int startStageIndex,
        string receiveAddress, string privateKey, FeeRate feeRate,
        Func<ProjectScripts, WitScript> buildWitScriptWithSigPlaceholder,
        Func<WitScript, TaprootSignature, WitScript> addSignatureToWitScript);
}
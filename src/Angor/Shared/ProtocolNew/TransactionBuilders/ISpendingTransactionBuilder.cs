using NBitcoin;
using Transaction = Blockcore.Consensus.TransactionInfo.Transaction;

namespace Angor.Shared.ProtocolNew.TransactionBuilders;

public interface ISpendingTransactionBuilder
{
    Transaction BuildRecoverInvestorRemainingFundsInProject(string investmentTransactionHex, ProjectInfo projectInfo, int startStage,
        string receiveAddress, string privateKey, FeeRate feeRate,
        Func<ProjectScripts, WitScript> buildWitScriptWithSigPlaceholder,
        Func<WitScript, TaprootSignature, WitScript> addSignatureToWitScript);
}
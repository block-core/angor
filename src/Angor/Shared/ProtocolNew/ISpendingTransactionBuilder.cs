using NBitcoin;
using Transaction = Blockcore.Consensus.TransactionInfo.Transaction;

namespace Angor.Shared.ProtocolNew;

public interface ISpendingTransactionBuilder
{
    Transaction RecoverProjectFunds(string investmentTransactionHex, ProjectInfo projectInfo, int startStage,
        string receiveAddress, string privateKey, FeeRate feeRate,
        Func<ProjectScripts, WitScript> buildWitScriptWithSigPlaceholder,
        Func<WitScript, TaprootSignature, WitScript> addSignatureToWitScript);
}
using Angor.Shared.Models;
using NBitcoin;

namespace Angor.Shared.Protocol.TransactionBuilders;

public interface ISpendingTransactionBuilder
{
    TransactionInfo BuildRecoverInvestorRemainingFundsInProject(string investmentTransactionHex, ProjectInfo projectInfo, int startStageIndex,
        string receiveAddress, AngorKey privateKey, FeeRate feeRate,
        Func<ProjectScripts, WitScript> buildWitScriptWithSigPlaceholder,
        Func<WitScript, TaprootSignature, WitScript> addSignatureToWitScript);
}
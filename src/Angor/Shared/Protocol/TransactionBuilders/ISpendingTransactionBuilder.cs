using Angor.Shared.Models;
using NBitcoin;

namespace Angor.Shared.Protocol.TransactionBuilders;

public interface ISpendingTransactionBuilder
{
    TransactionInfo BuildRecoverInvestorRemainingFundsInProject(string investmentTransactionHex, ProjectInfo projectInfo, int startStageIndex,
        string receiveAddress, string privateKey, FeeRate feeRate,
        Func<ProjectScripts, WitScript> buildWitScriptWithSigPlaceholder,
        Func<WitScript, TaprootSignature, WitScript> addSignatureToWitScript,
        DateTime? expiryDateOverride = null);
}
using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;

namespace Angor.Shared.ProtocolNew.Scripts;

public interface IInvestmentScriptBuilder
{
    Script GetInvestorPenaltyTransactionScript(string investorKey, int punishmentLockDays);

    ProjectScripts BuildProjectScriptsForStage(ProjectInfo projectInfo, string investorKey, int stageIndex,
        uint256? hashOfSecret = null);
}
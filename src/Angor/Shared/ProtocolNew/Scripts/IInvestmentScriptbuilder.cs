using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;

namespace Angor.Shared.ProtocolNew.Scripts;

public interface IInvestmentScriptBuilder
{
    Script GetInvestorPenaltyTransactionScript(string investorKey, DateTime punishmentLockTime);

    ProjectScripts BuildProjectScriptsForStage(ProjectInfo projectInfo, string investorKey, int stageIndex,
        string? hashOfSecret = null);
}
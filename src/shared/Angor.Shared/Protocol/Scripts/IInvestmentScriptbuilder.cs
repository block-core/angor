using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;

namespace Angor.Shared.Protocol.Scripts;

public interface IInvestmentScriptBuilder
{
    Script GetInvestorPenaltyTransactionScript(string investorKey, int punishmentLockDays);

    ProjectScripts BuildProjectScriptsForStage(ProjectInfo projectInfo, FundingParameters parameters, int stageIndex);
}
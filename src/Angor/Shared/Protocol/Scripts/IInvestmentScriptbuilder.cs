using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;

namespace Angor.Shared.Protocol.Scripts;

public interface IInvestmentScriptBuilder
{
    Script GetInvestorPenaltyTransactionScript(string investorKey, int punishmentLockDays);

    ProjectScripts BuildProjectScriptsForStage(ProjectInfo projectInfo, FundingParameters parameters, int stageIndex);

    [Obsolete("Use BuildProjectScriptsForStage(ProjectInfo, FundingParameters) instead")]
    ProjectScripts BuildProjectScriptsForStage(ProjectInfo projectInfo, string investorKey, int stageIndex,
        uint256? hashOfSecret = null, DateTime? expiryDateOverride = null);
  
    [Obsolete("Use BuildProjectScriptsForStage(ProjectInfo, FundingParameters) instead")]
    ProjectScripts BuildProjectScriptsForStage(ProjectInfo projectInfo, string investorKey, int stageIndex,
    uint256? hashOfSecret, DateTime? expiryDateOverride, DateTime? investmentStartDate, byte patternIndex = 0);
}
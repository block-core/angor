using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;

namespace Angor.Shared.Protocol.Scripts;

public interface IInvestmentScriptBuilder
{
    Script GetInvestorPenaltyTransactionScript(string investorKey, int punishmentLockDays);

    ProjectScripts BuildProjectScriptsForStage(ProjectInfo projectInfo, string investorKey, int stageIndex,
        uint256? hashOfSecret = null, DateTime? expiryDateOverride = null);
        
    /// <summary>
    /// Builds project scripts for a specific stage, supporting both Invest (fixed) and Fund/Subscribe (dynamic) projects.
    /// </summary>
    /// <param name="investmentStartDate">Required for Fund/Subscribe projects - the date when investment was made</param>
    /// <param name="patternIndex">Index of the pattern in DynamicStagePatterns list (0-255). Only used for Fund/Subscribe projects.</param>
    ProjectScripts BuildProjectScriptsForStage(ProjectInfo projectInfo, string investorKey, int stageIndex,
        uint256? hashOfSecret, DateTime? expiryDateOverride, DateTime? investmentStartDate, byte patternIndex = 0);
}
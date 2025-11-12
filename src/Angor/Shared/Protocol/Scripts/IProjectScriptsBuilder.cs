using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;

namespace Angor.Shared.Protocol.Scripts;

public interface IProjectScriptsBuilder
{
    Script GetAngorFeeOutputScript(string angorKey);
    
    /// <summary>
    /// Builds investor info script with support for dynamic stages.
    /// For Invest projects: includes only investor key.
    /// For Fund/Subscribe projects: includes investor key + encoded dynamic stage info.
    /// </summary>
    Script BuildInvestorInfoScript(string investorKey, ProjectInfo projectInfo, DateTime? investmentStartDate = null);
    
    Script BuildFounderInfoScript(string founderKey, short keyType, string nostrEventId);

    /// <summary>
    /// Builds seeder info script with support for dynamic stages.
    /// For Invest projects: includes investor key + secret hash.
    /// For Fund/Subscribe projects: includes investor key + secret hash + encoded dynamic stage info.
    /// </summary>
    Script BuildSeederInfoScript(string investorKey, uint256 secretHash, ProjectInfo projectInfo, DateTime? investmentStartDate = null);
    
    (string investorKey, uint256? secretHash) GetInvestmentDataFromOpReturnScript(Script script);
    
    /// <summary>
    /// Extracts dynamic stage info from an OP_RETURN script if present.
    /// </summary>
    /// <returns>DynamicStageInfo if present, null otherwise</returns>
    DynamicStageInfo? GetDynamicStageInfoFromOpReturnScript(Script script);
}
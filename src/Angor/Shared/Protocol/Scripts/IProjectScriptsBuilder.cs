using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;

namespace Angor.Shared.Protocol.Scripts;

public interface IProjectScriptsBuilder
{
    Script GetAngorFeeOutputScript(string angorKey);

    Script BuildInvestorInfoScript(ProjectInfo projectInfo, FundingParameters parameters);

    Script BuildFounderInfoScript(string founderKey, short keyType, string nostrEventId);

    Script BuildSeederInfoScript(ProjectInfo projectInfo, FundingParameters parameters);

    (string investorKey, uint256? secretHash) GetInvestmentDataFromOpReturnScript(Script script);

    DynamicStageInfo? GetDynamicStageInfoFromOpReturnScript(Script script);
}
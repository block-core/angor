using Angor.Shared.Models;
using NBitcoin;
using NBitcoin;

namespace Angor.Shared.Protocol.Scripts;

public interface IInvestmentScriptBuilder
{
    Script GetInvestorPenaltyTransactionScript(string investorKey, int punishmentLockDays);

    ProjectScripts BuildProjectScriptsForStage(ProjectInfo projectInfo, FundingParameters parameters, int stageIndex);
}
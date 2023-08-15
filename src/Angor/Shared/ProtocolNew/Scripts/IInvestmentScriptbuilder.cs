using Blockcore.Consensus.ScriptInfo;

namespace Angor.Shared.ProtocolNew.Scripts;

public interface IInvestmentScriptBuilder
{
    Script GetInvestorPenaltyTransactionScript(string investorKey, DateTime punishmentLockTime);
    
    ProjectScripts BuildSSeederScripts(string funderKey, string investorKey, DateTime founderLockTime,
        DateTime projectExpieryLocktime, string? secretHash);

    ProjectScripts BuildInvestorScripts(string funderKey, string investorKey, DateTime founderLockTime,
        DateTime projectExpieryLocktime, ProjectSeeders seeders);
}
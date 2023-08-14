namespace Angor.Shared.ProtocolNew.Scripts;

public interface IInvestmentScriptBuilder
{
    ProjectScripts BuildSSeederScripts(string funderKey, string investorKey, string? secretHash, DateTime founderLockTime,
        DateTime projectExpieryLocktime);

    ProjectScripts BuildInvestorScripts(string funderKey, string investorKey, DateTime founderLockTime,
        DateTime projectExpieryLocktime, ProjectSeeders seeders);
}
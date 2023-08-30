using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;

namespace Angor.Shared.ProtocolNew.Scripts;

public class InvestmentScriptBuilder : IInvestmentScriptBuilder
{
    private readonly ISeederScriptTreeBuilder _seederScriptTreeBuilder;

    public InvestmentScriptBuilder(ISeederScriptTreeBuilder seederScriptTreeBuilder)
    {
        _seederScriptTreeBuilder = seederScriptTreeBuilder;
    }

    public Script GetInvestorPenaltyTransactionScript(string investorKey, DateTime punishmentLockTime)
    {
        var unixTime = Utils.DateTimeToUnixTime(punishmentLockTime);
        
        return new(new List<Op>
        {
            Op.GetPushOp(new NBitcoin.PubKey(investorKey).ToBytes()),
            OpcodeType.OP_CHECKSIGVERIFY,
            Op.GetPushOp(unixTime),
            OpcodeType.OP_CHECKLOCKTIMEVERIFY
        });
    }

    public ProjectScripts BuildProjectScriptsForStage(ProjectInfo projectInfo, string investorKey, int stageIndex,
        string? hashOfSecret)
    {
        // regular investor pre-co-sign with founder to gets funds with penalty
        var recoveryOps = new List<Op>
        {
            Op.GetPushOp(new NBitcoin.PubKey(projectInfo.FounderKey).GetTaprootFullPubKey().ToBytes()),
            OpcodeType.OP_CHECKSIGVERIFY,
            Op.GetPushOp(new NBitcoin.PubKey(investorKey).GetTaprootFullPubKey().ToBytes()),
        };

        var secretHashOps = string.IsNullOrEmpty(hashOfSecret)
            ? new List<Op> { OpcodeType.OP_CHECKSIG }
            : new List<Op>
            {
                OpcodeType.OP_CHECKSIGVERIFY,
                OpcodeType.OP_HASH256,
                Op.GetPushOp(new uint256(hashOfSecret).ToBytes()),
                OpcodeType.OP_EQUAL
            };
        
        recoveryOps.AddRange(secretHashOps);

        var seeders = string.IsNullOrEmpty(hashOfSecret) && projectInfo.ProjectSeeders.SecretHashes.Any()
            ? _seederScriptTreeBuilder.BuildSeederScriptTree(investorKey,
                projectInfo.ProjectSeeders.Threshold,
                projectInfo.ProjectSeeders.SecretHashes).ToList()
            : new List<Script>();
        
        return new()
        {
            Founder = GetFounderSpendScript(projectInfo.FounderKey, projectInfo.Stages[stageIndex].ReleaseDate),
            Recover = new Script(recoveryOps),
            EndOfProject = GetEndOfProjectInvestorSpendScript(investorKey, projectInfo.ExpiryDate),
            Seeders = seeders
        };
    }

    private static Script GetFounderSpendScript(string funderKey, DateTime stageReleaseDate)
    {
        long locktimeFounder = Utils.DateTimeToUnixTime(stageReleaseDate);   
        
        // funder gets funds after stage started
        return new Script(new List<Op>
        {
            Op.GetPushOp(new NBitcoin.PubKey(funderKey).GetTaprootFullPubKey().ToBytes()),
            OpcodeType.OP_CHECKSIGVERIFY,
            Op.GetPushOp(locktimeFounder),
            OpcodeType.OP_CHECKLOCKTIMEVERIFY
        });
    }

    private static Script GetEndOfProjectInvestorSpendScript(string investorKey, DateTime projectExpieryDate)
    {
        long locktimeExpiery = Utils.DateTimeToUnixTime(projectExpieryDate);
        
        // project ended and investor can collect remaining funds
        return new Script(new List<Op>
        {
            Op.GetPushOp(new NBitcoin.PubKey(investorKey).GetTaprootFullPubKey().ToBytes()),
            OpcodeType.OP_CHECKSIGVERIFY,
            Op.GetPushOp(locktimeExpiery),
            OpcodeType.OP_CHECKLOCKTIMEVERIFY
        });
    }
}
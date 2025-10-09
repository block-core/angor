using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;

namespace Angor.Shared.Protocol.Scripts;

public class InvestmentScriptBuilder : IInvestmentScriptBuilder
{
    private readonly ISeederScriptTreeBuilder _seederScriptTreeBuilder;

    public InvestmentScriptBuilder(ISeederScriptTreeBuilder seederScriptTreeBuilder)
    {
        _seederScriptTreeBuilder = seederScriptTreeBuilder;
    }

    public Script GetInvestorPenaltyTransactionScript(string investorKey, int punishmentLockDays)
    {
        if (punishmentLockDays > 388)
        {
            // the actual number is 65535*512 seconds (388 days) 
            // https://en.bitcoin.it/wiki/Timelock
            throw new ArgumentOutOfRangeException(nameof(punishmentLockDays), $"Invalid CSV value {punishmentLockDays}");
        }
        
        var sequence = new Sequence(TimeSpan.FromDays(punishmentLockDays));

        return new(new List<Op>
        {
            Op.GetPushOp(new NBitcoin.PubKey(investorKey).ToBytes()),
            OpcodeType.OP_CHECKSIGVERIFY,
            Op.GetPushOp((uint)sequence),
            OpcodeType.OP_CHECKSEQUENCEVERIFY
        });
    }

    public ProjectScripts BuildProjectScriptsForStage(ProjectInfo projectInfo, string investorKey, int stageIndex, uint256? hashOfSecret)
    {
        return BuildProjectScriptsForStage(projectInfo, investorKey, stageIndex, hashOfSecret, false);
    }

    public ProjectScripts BuildProjectScriptsForStage(ProjectInfo projectInfo, string investorKey, int stageIndex, uint256? hashOfSecret, bool disablePenalty)
    {
        List<Op> recoveryOps;

        if (disablePenalty)
        {
            // regular investor without penalty no co-sign needed
            recoveryOps = new List<Op>
            {
                Op.GetPushOp(new NBitcoin.PubKey(investorKey).GetTaprootFullPubKey().ToBytes()),
                OpcodeType.OP_CHECKSIG
            };
        }
        else
        {
            // regular investor pre-co-sign with founder to gets funds with penalty
            recoveryOps = new List<Op>
            {
                Op.GetPushOp(new NBitcoin.PubKey(projectInfo.FounderRecoveryKey).GetTaprootFullPubKey().ToBytes()),
                OpcodeType.OP_CHECKSIGVERIFY,
                Op.GetPushOp(new NBitcoin.PubKey(investorKey).GetTaprootFullPubKey().ToBytes()),
            };

            var secretHashOps = hashOfSecret == null
                ? new List<Op> { OpcodeType.OP_CHECKSIG }
                : new List<Op>
                {
                    OpcodeType.OP_CHECKSIGVERIFY,
                    OpcodeType.OP_HASH256,
                    Op.GetPushOp(new uint256(hashOfSecret).ToBytes()),
                    OpcodeType.OP_EQUAL
                };

            recoveryOps.AddRange(secretHashOps);
        }

        var seeders = hashOfSecret == null && projectInfo.ProjectSeeders.SecretHashes.Any()
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

    private static Script GetFounderSpendScript(string founderKey, DateTime stageReleaseDate)
    {
        long locktimeFounder = Utils.DateTimeToUnixTime(stageReleaseDate);   
        
        // founder gets funds after stage started
        return new Script(new List<Op>
        {
            Op.GetPushOp(new NBitcoin.PubKey(founderKey).GetTaprootFullPubKey().ToBytes()),
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
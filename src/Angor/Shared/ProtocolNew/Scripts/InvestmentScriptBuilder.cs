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

    public ProjectScripts BuildSSeederScripts(string funderKey, string investorKey, DateTime founderLockTime, 
        DateTime projectExpieryLocktime, string? secretHash)
    {
        long locktimeFounder = Utils.DateTimeToUnixTime(founderLockTime);
        long locktimeExpiery = Utils.DateTimeToUnixTime(projectExpieryLocktime);

        return new()
        {
            // funder gets funds after stage started
            Founder = GetFounderSpendScript(funderKey, locktimeFounder),
            //  seed investor pre-co-sign with founder to gets funds with penalty and must expose the secret
            Recover = new Script(new List<Op>
            {
                Op.GetPushOp(new NBitcoin.PubKey(funderKey).GetTaprootFullPubKey().ToBytes()),
                OpcodeType.OP_CHECKSIGVERIFY,
                Op.GetPushOp(new NBitcoin.PubKey(investorKey).GetTaprootFullPubKey().ToBytes()),
                OpcodeType.OP_CHECKSIGVERIFY,
                OpcodeType.OP_HASH256,
                Op.GetPushOp(new uint256(secretHash).ToBytes()),
                OpcodeType.OP_EQUAL
            }),
            // project ended and investor can collect remaining funds
            EndOfProject = GetEndOfProjectInvestorSpendScript(investorKey, locktimeExpiery)
        };
    }

    public ProjectScripts BuildInvestorScripts(string funderKey, string investorKey, DateTime founderLockTime,
        DateTime projectExpieryLocktime, ProjectSeeders seeders)
    {
        long locktimeFounder = Utils.DateTimeToUnixTime(founderLockTime);
        long locktimeExpiery = Utils.DateTimeToUnixTime(projectExpieryLocktime);

        ProjectScripts projectScripts = new()
        {

            // funder gets funds after stage started
            Founder = GetFounderSpendScript(funderKey, locktimeFounder),
            // regular investor pre-co-sign with founder to gets funds with penalty
            Recover = new Script(new List<Op>
            {
                Op.GetPushOp(new NBitcoin.PubKey(funderKey).GetTaprootFullPubKey().ToBytes()),
                OpcodeType.OP_CHECKSIGVERIFY,
                Op.GetPushOp(new NBitcoin.PubKey(investorKey).GetTaprootFullPubKey().ToBytes()),
                OpcodeType.OP_CHECKSIG
            }),
            // project ended and investor can collect remaining funds
            EndOfProject = GetEndOfProjectInvestorSpendScript(investorKey, locktimeExpiery)
        };

        if (seeders.SecretHashes.Any())
        {
            // all the combinations of penalty free recovery based on a threshold of seeder secret hashes
            var seederHashes = _seederScriptTreeBuilder.BuildSeederScriptTree(investorKey, seeders.Threshold,
                seeders.SecretHashes);

            projectScripts.Seeders.AddRange(seederHashes);
        }

        return projectScripts;
    }

    private static Script GetFounderSpendScript(string funderKey, long locktimeFounder)
    {
        return new Script(new List<Op>
        {
            Op.GetPushOp(new NBitcoin.PubKey(funderKey).GetTaprootFullPubKey().ToBytes()),
            OpcodeType.OP_CHECKSIGVERIFY,
            Op.GetPushOp(locktimeFounder),
            OpcodeType.OP_CHECKLOCKTIMEVERIFY
        });
    }

    private static Script GetEndOfProjectInvestorSpendScript(string investorKey, long locktimeExpiery)
    {
        return new Script(new List<Op>
        {
            Op.GetPushOp(new NBitcoin.PubKey(investorKey).GetTaprootFullPubKey().ToBytes()),
            OpcodeType.OP_CHECKSIGVERIFY,
            Op.GetPushOp(locktimeExpiery),
            OpcodeType.OP_CHECKLOCKTIMEVERIFY
        });
    }
}
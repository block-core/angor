using Angor.Shared.Models;
using Angor.Shared.Utilities;
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

    public ProjectScripts BuildProjectScriptsForStage(ProjectInfo projectInfo, FundingParameters parameters, int stageIndex)
    {
        parameters.Validate(projectInfo, stageIndex);

        DateTime stageReleaseDate;

        if (projectInfo.ProjectType == ProjectType.Invest)
        {
            stageReleaseDate = projectInfo.Stages[stageIndex].ReleaseDate;
        }
        else
        {
            var pattern = parameters.FindPattern(projectInfo);
            stageReleaseDate = CalculateDynamicStageReleaseDate(parameters.InvestmentStartDate.Value, pattern, stageIndex);
        }

        var recoveryOps = new List<Op>
        {
            Op.GetPushOp(new NBitcoin.PubKey(projectInfo.FounderRecoveryKey).GetTaprootFullPubKey().ToBytes()),
            OpcodeType.OP_CHECKSIGVERIFY,
            Op.GetPushOp(new NBitcoin.PubKey(parameters.InvestorKey).GetTaprootFullPubKey().ToBytes()),
        };

        var secretHashOps = parameters.HashOfSecret == null
           ? new List<Op> { OpcodeType.OP_CHECKSIG }
          : new List<Op>
            {
                OpcodeType.OP_CHECKSIGVERIFY,
                OpcodeType.OP_HASH256,
                Op.GetPushOp(new uint256(parameters.HashOfSecret).ToBytes()),
                OpcodeType.OP_EQUAL
            };

        recoveryOps.AddRange(secretHashOps);

        var seeders = parameters.HashOfSecret == null && projectInfo.ProjectSeeders.SecretHashes.Any()
            ? _seederScriptTreeBuilder.BuildSeederScriptTree(parameters.InvestorKey,
                projectInfo.ProjectSeeders.Threshold,
                projectInfo.ProjectSeeders.SecretHashes).ToList()
            : new List<Script>();

        var effectiveExpiryDate = parameters.ExpiryDateOverride ?? projectInfo.ExpiryDate;

        return new()
        {
            Founder = GetFounderSpendScript(projectInfo.FounderKey, stageReleaseDate),
            Recover = new Script(recoveryOps),
            EndOfProject = GetEndOfProjectInvestorSpendScript(parameters.InvestorKey, effectiveExpiryDate),
            Seeders = seeders
        };
    }

    private static DateTime CalculateDynamicStageReleaseDate(DateTime investmentStartDate, DynamicStagePattern pattern, int stageIndex)
    {
        return DynamicStageCalculator.CalculateDynamicStageReleaseDate(investmentStartDate, pattern, stageIndex);
    }

    private static Script GetFounderSpendScript(string founderKey, DateTime stageReleaseDate)
    {
        long locktimeFounder = Utils.DateTimeToUnixTime(stageReleaseDate);

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

        return new Script(new List<Op>
        {
            Op.GetPushOp(new NBitcoin.PubKey(investorKey).GetTaprootFullPubKey().ToBytes()),
            OpcodeType.OP_CHECKSIGVERIFY,
            Op.GetPushOp(locktimeExpiery),
            OpcodeType.OP_CHECKLOCKTIMEVERIFY
        });
    }
}
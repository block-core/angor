using Angor.Shared.Models;
using Blockcore.NBitcoin.DataEncoders;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Op = Blockcore.Consensus.ScriptInfo.Op;
using OpcodeType = Blockcore.Consensus.ScriptInfo.OpcodeType;
using Script = Blockcore.Consensus.ScriptInfo.Script;
using Sequence = Blockcore.NBitcoin.Sequence;
using uint256 = Blockcore.NBitcoin.uint256;
using Utils = Blockcore.NBitcoin.Utils;

namespace Angor.Shared.Protocol.Scripts;

public class InvestmentScriptBuilder : IInvestmentScriptBuilder
{
    private readonly ILogger<InvestmentScriptBuilder>? _logger;
    private readonly ISeederScriptTreeBuilder _seederScriptTreeBuilder;

    public InvestmentScriptBuilder(ISeederScriptTreeBuilder seederScriptTreeBuilder, ILogger<InvestmentScriptBuilder>? logger = null)
    {
        _seederScriptTreeBuilder = seederScriptTreeBuilder;
        _logger = logger;
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

    public ProjectScripts BuildProjectScriptsForStage(ProjectInfo projectInfo, string investorKey, int stageIndex,
        uint256? hashOfSecret)
    {
        _logger?.LogInformation($"founderFullPubKey {new NBitcoin.PubKey(projectInfo.FounderRecoveryKey).ToHex()}");
        var taprootFullPubKey = new NBitcoin.PubKey(projectInfo.FounderRecoveryKey).GetTaprootFullPubKey();
        _logger?.LogInformation($"founderFullPubKey taproot {taprootFullPubKey}");
        
        _logger?.LogInformation($"taproot 1: {Encoders.Hex.EncodeData(taprootFullPubKey.OutputKey.ToBytes())}");
        _logger?.LogInformation($"investor 2: {Encoders.Hex.EncodeData(taprootFullPubKey.ToBytes())}");
        _logger?.LogInformation($"investor 3: {Encoders.Hex.EncodeData(taprootFullPubKey.InternalKey.GetTaprootFullPubKey().ToBytes())}");
        _logger?.LogInformation($"investor 4: {Encoders.Hex.EncodeData(taprootFullPubKey.InternalKey.GetTaprootFullPubKey().OutputKey.ToBytes())}");
        _logger?.LogInformation($"investor 3: {Encoders.Hex.EncodeData(taprootFullPubKey.InternalKey.AsTaprootPubKey().ToBytes())}");
        
        _logger?.LogInformation($"Environment: {Environment.MachineName}, {Environment.Version}");
        
        _logger?.LogInformation($"investor 3: {Encoders.Hex.EncodeData(taprootFullPubKey.ScriptPubKey.ToBytes())}");
        _logger?.LogInformation($"investor 3: {taprootFullPubKey.ScriptPubKey.ToHex()}");
        _logger?.LogInformation($"investor 3: {Encoders.Hex.EncodeData(taprootFullPubKey.ScriptPubKey.ToCompressedBytes())}");
        _logger?.LogInformation($"investor 3: {Encoders.Hex.EncodeData(taprootFullPubKey.ScriptPubKey.ToTapScript(TapLeafVersion.C0).Script.ToBytes())}");
        
        
        // regular investor pre-co-sign with founder to gets funds with penalty
        var recoveryOps = new List<Op>
        {
            Op.GetPushOp(new NBitcoin.PubKey(projectInfo.FounderRecoveryKey).GetTaprootFullPubKey().ToBytes()),
            OpcodeType.OP_CHECKSIGVERIFY,
            Op.GetPushOp(new NBitcoin.PubKey(investorKey).GetTaprootFullPubKey().ToBytes()),
        };

        _logger?.LogInformation(
            $"recovery ops:{Encoders.Hex.EncodeData(recoveryOps.SelectMany(op => op.ToBytes()).ToArray())}");
        
        
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

        _logger?.LogInformation(
            $"recovery ops + hashes:{Encoders.Hex.EncodeData(recoveryOps.SelectMany(op => op.ToBytes()).ToArray())}");
        
        var seeders = hashOfSecret == null && projectInfo.ProjectSeeders.SecretHashes.Any()
            ? _seederScriptTreeBuilder.BuildSeederScriptTree(investorKey,
                projectInfo.ProjectSeeders.Threshold,
                projectInfo.ProjectSeeders.SecretHashes).ToList()
            : new List<Script>();
        
        var result = new ProjectScripts()
        {
            Founder = GetFounderSpendScript(projectInfo.FounderKey, projectInfo.Stages[stageIndex].ReleaseDate),
            Recover = new Script(recoveryOps),
            EndOfProject = GetEndOfProjectInvestorSpendScript(investorKey, projectInfo.ExpiryDate),
            Seeders = seeders
        };
        return result;
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
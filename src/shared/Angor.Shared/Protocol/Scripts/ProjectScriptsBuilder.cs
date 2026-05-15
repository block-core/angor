using System.Buffers.Binary;
using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;

namespace Angor.Shared.Protocol.Scripts;

public class ProjectScriptsBuilder : IProjectScriptsBuilder
{
    private readonly IDerivationOperations _derivationOperations;

    public ProjectScriptsBuilder(IDerivationOperations derivationOperations)
    {
        _derivationOperations = derivationOperations;
    }

    private static byte[] BitConverterToLittleEndian(short value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
        return bytes;
    }

    public Script GetAngorFeeOutputScript(string angorKey)
    {
        return _derivationOperations.AngorKeyToScript(angorKey);
    }

    public Script BuildInvestorInfoScript(ProjectInfo projectInfo, FundingParameters parameters)
    {
        var ops = new List<Op>
        {
            OpcodeType.OP_RETURN,
            Op.GetPushOp(new PubKey(parameters.InvestorKey).ToBytes())
        };

        // For Fund/Subscribe projects, add dynamic stage info
        if (projectInfo.ProjectType == ProjectType.Fund || projectInfo.ProjectType == ProjectType.Subscribe)
        {
            // Use the specified pattern
            var pattern = parameters.FindPattern(projectInfo);
            var dynamicInfo = DynamicStageInfo.FromPattern(pattern, parameters);

            ops.Add(Op.GetPushOp(dynamicInfo.ToBytes()));
        }

        return new Script(ops);
    }

    public Script BuildFounderInfoScript(string founderKey, short keyType, string nostrEventId)
    {
        return new Script(OpcodeType.OP_RETURN,
            Op.GetPushOp(new PubKey(founderKey).ToBytes()),
            Op.GetPushOp(BitConverterToLittleEndian(keyType)),
            Op.GetPushOp(Encoders.Hex.DecodeData(nostrEventId)));
    }

    public Script BuildSeederInfoScript(ProjectInfo projectInfo, FundingParameters parameters)
    {
        var ops = new List<Op>
        {
            OpcodeType.OP_RETURN,
            Op.GetPushOp(new PubKey(parameters.InvestorKey).ToBytes()),
            Op.GetPushOp((parameters.HashOfSecret ?? new uint256(0)).ToBytes())
        };

        // For Fund/Subscribe projects, add dynamic stage info
        if (projectInfo.ProjectType == ProjectType.Fund || projectInfo.ProjectType == ProjectType.Subscribe)
        {
            // Use the specified pattern
            var pattern = parameters.FindPattern(projectInfo);
            var dynamicInfo = DynamicStageInfo.FromPattern(pattern, parameters);

            ops.Add(Op.GetPushOp(dynamicInfo.ToBytes()));
        }

        return new Script(ops);
    }

    public (string investorKey, uint256? secretHash) GetInvestmentDataFromOpReturnScript(Script script)
    {
        if (!script.IsUnspendable)
        {
            throw new Exception("Script is not an OP_RETURN script");
        }

        var ops = script.ToOps();

        // Validate minimum structure: OP_RETURN + at least one push
        if (ops.Count < 2 || ops[1].PushData == null)
            throw new Exception($"Unexpected OP_RETURN format: expected at least 2 operations with push data, got {ops.Count}");

        // Validate investor key is a valid compressed public key (33 bytes)
        if (ops[1].PushData.Length != 33)
            throw new Exception($"Invalid investor key length in OP_RETURN: expected 33 bytes (compressed pubkey), got {ops[1].PushData.Length}");

        if (ops.Count == 2)
        {
            // Invest project: investor key only
            return (new PubKey(ops[1].PushData).ToHex(), null);
        }

        if (ops.Count == 3)
        {
            // Could be:
            // 1. Invest project with seeder: investor key + secret hash (32 bytes)
            // 2. Fund/Subscribe project: investor key + dynamic info (4 bytes)

            if (ops[2].PushData == null)
                throw new Exception("Unexpected OP_RETURN format: third operation has no push data");

            if (ops[2].PushData.Length == 32)
            {
                // Seeder with secret hash
                PubKey pubKey = new PubKey(ops[1].PushData);
                uint256 secretHash = new uint256(ops[2].PushData);
                return (pubKey.ToHex(), secretHash);
            }
            else if (ops[2].PushData.Length == 4)
            {
                // Dynamic stage info (no secret hash)
                return (new PubKey(ops[1].PushData).ToHex(), null);
            }
            else
            {
                throw new Exception($"Unexpected OP_RETURN format: third push data has unrecognized length {ops[2].PushData.Length} (expected 32 for secret hash or 4 for dynamic info)");
            }
        }

        if (ops.Count == 4)
        {
            // Fund/Subscribe seeder: investor key + secret hash + dynamic info
            if (ops[2].PushData?.Length != 32)
                throw new Exception($"Unexpected OP_RETURN format: expected 32-byte secret hash at position 2, got {ops[2].PushData?.Length}");
            if (ops[3].PushData?.Length != 4)
                throw new Exception($"Unexpected OP_RETURN format: expected 4-byte dynamic info at position 3, got {ops[3].PushData?.Length}");

            PubKey pubKey = new PubKey(ops[1].PushData);
            uint256 secretHash = new uint256(ops[2].PushData);
            return (pubKey.ToHex(), secretHash);
        }

        throw new Exception($"Unexpected OP_RETURN format with {ops.Count} operations");
    }

    public DynamicStageInfo? GetDynamicStageInfoFromOpReturnScript(Script script)
    {
        if (!script.IsUnspendable)
        {
            return null;
        }

        var ops = script.ToOps();

        // Check for dynamic stage info in different positions
        if (ops.Count == 3 && ops[2].PushData?.Length == 4)
        {
            // Investor with dynamic info: [OP_RETURN] [investor key] [dynamic info]
            return DynamicStageInfo.FromBytes(ops[2].PushData);
        }

        if (ops.Count == 4 && ops[3].PushData?.Length == 4)
        {
            // Seeder with dynamic info: [OP_RETURN] [investor key] [secret hash] [dynamic info]
            return DynamicStageInfo.FromBytes(ops[3].PushData);
        }

        return null;
    }
}
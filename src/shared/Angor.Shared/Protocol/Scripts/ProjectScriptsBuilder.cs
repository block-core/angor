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
        };

        // V3+: prepend a 1-byte protocol version marker for unambiguous parsing
        if (projectInfo.Version >= 3)
        {
            ops.Add(Op.GetPushOp(new byte[] { (byte)projectInfo.Version }));
        }

        ops.Add(Op.GetPushOp(new PubKey(parameters.InvestorKey).ToBytes()));

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
        };

        // V3+: prepend a 1-byte protocol version marker for unambiguous parsing
        if (projectInfo.Version >= 3)
        {
            ops.Add(Op.GetPushOp(new byte[] { (byte)projectInfo.Version }));
        }

        ops.Add(Op.GetPushOp(new PubKey(parameters.InvestorKey).ToBytes()));
        ops.Add(Op.GetPushOp((parameters.HashOfSecret ?? new uint256(0)).ToBytes()));

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

        // Detect V3+ format: first push after OP_RETURN is a 1-byte version marker
        int dataStartIndex = 1; // default: data starts at ops[1]
        if (ops[1].PushData.Length == 1 && ops[1].PushData[0] >= 3)
        {
            // V3+ protocol: skip the version byte
            dataStartIndex = 2;
            if (ops.Count < 3 || ops[2].PushData == null)
                throw new Exception($"V3 OP_RETURN: expected investor key after version byte, got {ops.Count - 1} data pushes");
        }

        // Validate investor key is a valid compressed public key (33 bytes)
        if (ops[dataStartIndex].PushData.Length != 33)
            throw new Exception($"Invalid investor key length in OP_RETURN: expected 33 bytes (compressed pubkey), got {ops[dataStartIndex].PushData.Length}");

        var remainingOps = ops.Count - dataStartIndex;

        if (remainingOps == 1)
        {
            // Invest project: investor key only
            return (new PubKey(ops[dataStartIndex].PushData).ToHex(), null);
        }

        if (remainingOps == 2)
        {
            // Could be:
            // 1. Invest project with seeder: investor key + secret hash (32 bytes)
            // 2. Fund/Subscribe project: investor key + dynamic info (4 bytes)

            var nextIdx = dataStartIndex + 1;
            if (ops[nextIdx].PushData == null)
                throw new Exception("Unexpected OP_RETURN format: push data is null after investor key");

            if (ops[nextIdx].PushData.Length == 32)
            {
                // Seeder with secret hash
                PubKey pubKey = new PubKey(ops[dataStartIndex].PushData);
                uint256 secretHash = new uint256(ops[nextIdx].PushData);
                return (pubKey.ToHex(), secretHash);
            }
            else if (ops[nextIdx].PushData.Length == 4)
            {
                // Dynamic stage info (no secret hash)
                return (new PubKey(ops[dataStartIndex].PushData).ToHex(), null);
            }
            else
            {
                throw new Exception($"Unexpected OP_RETURN format: push data has unrecognized length {ops[nextIdx].PushData.Length} (expected 32 for secret hash or 4 for dynamic info)");
            }
        }

        if (remainingOps == 3)
        {
            // Fund/Subscribe seeder: investor key + secret hash + dynamic info
            var hashIdx = dataStartIndex + 1;
            var dynIdx = dataStartIndex + 2;
            if (ops[hashIdx].PushData?.Length != 32)
                throw new Exception($"Unexpected OP_RETURN format: expected 32-byte secret hash at position {hashIdx}, got {ops[hashIdx].PushData?.Length}");
            if (ops[dynIdx].PushData?.Length != 4)
                throw new Exception($"Unexpected OP_RETURN format: expected 4-byte dynamic info at position {dynIdx}, got {ops[dynIdx].PushData?.Length}");

            PubKey pubKey = new PubKey(ops[dataStartIndex].PushData);
            uint256 secretHash = new uint256(ops[hashIdx].PushData);
            return (pubKey.ToHex(), secretHash);
        }

        throw new Exception($"Unexpected OP_RETURN format with {ops.Count} operations (data starts at index {dataStartIndex})");
    }

    public DynamicStageInfo? GetDynamicStageInfoFromOpReturnScript(Script script)
    {
        if (!script.IsUnspendable)
        {
            return null;
        }

        var ops = script.ToOps();

        // Detect V3+ format: first push after OP_RETURN is a 1-byte version marker
        int dataStartIndex = 1;
        if (ops.Count >= 2 && ops[1].PushData?.Length == 1 && ops[1].PushData[0] >= 3)
        {
            dataStartIndex = 2; // skip version byte
        }

        var remainingOps = ops.Count - dataStartIndex;

        // Check for dynamic stage info: last push is 4 bytes
        if (remainingOps == 2 && ops[dataStartIndex + 1].PushData?.Length == 4)
        {
            // Investor with dynamic info: [investor key] [dynamic info]
            return DynamicStageInfo.FromBytes(ops[dataStartIndex + 1].PushData);
        }

        if (remainingOps == 3 && ops[dataStartIndex + 2].PushData?.Length == 4)
        {
            // Seeder with dynamic info: [investor key] [secret hash] [dynamic info]
            return DynamicStageInfo.FromBytes(ops[dataStartIndex + 2].PushData);
        }

        return null;
    }
}
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;

namespace Angor.Shared.ProtocolNew.Scripts;

public class SeederScriptTreeBuilder : ISeederScriptTreeBuilder
{
    public IEnumerable<Script> BuildSeederScriptTree(string investorKey, int seederThreshold, List<string> secretHashes)
    {
        List<Script> list = new();

        var thresholds = CreateThresholds(seederThreshold, secretHashes);

        foreach (var threshold in thresholds)
        {
            var ops = new List<Op>();

            foreach (var secretHash in threshold)
            {
                ops.AddRange(new []
                {
                    OpcodeType.OP_HASH256,
                    Op.GetPushOp(new uint256(secretHash).ToBytes()),
                    OpcodeType.OP_EQUALVERIFY
                });
            }

            ops.AddRange(new[]
            {
                Op.GetPushOp(new NBitcoin.PubKey(investorKey).GetTaprootFullPubKey().ToBytes()),
                OpcodeType.OP_CHECKSIG,
            });

            list.Add(new Script(ops));
        }

        return list;
    }
    
    public static List<List<string>> CreateThresholds(int threshold, List<string> secretHashes)
    {
        var result = new List<List<string>>();
        
        result.AddRange(GetCombinations(secretHashes, threshold));

        return result;
    }
    
    private static List<List<string>> GetCombinations(List<string> list, int length)
    {
        if (length == 1) return list.Select(item => new List<string> { item }).ToList();

        var combinations = new List<List<string>>();
        for (int i = 0; i < list.Count; i++)
        {
            var subCombinations = GetCombinations(list.Skip(i + 1).ToList(), length - 1);
            foreach (var subCombination in subCombinations)
            {
                var combination = new List<string> { list[i] };
                combination.AddRange(subCombination);
                combinations.Add(combination);
            }
        }

        return combinations;
    }
}
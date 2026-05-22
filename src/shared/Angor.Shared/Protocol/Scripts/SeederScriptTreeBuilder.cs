using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;

namespace Angor.Shared.Protocol.Scripts;

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
                Op.GetPushOp(TaprootKeyHelper.GetTaprootOutputKeyBytes(investorKey)),
                OpcodeType.OP_CHECKSIG,
            });

            list.Add(new Script(ops));
        }

        return list;
    }
    
    public static List<List<string>> CreateThresholds(int threshold, List<string> secretHashes)
    {
        // Guard against combinatorial explosion: C(n,k) can grow very large.
        // Cap at a reasonable limit to prevent OOM/DoS.
        const int maxCombinations = 10_000;
        long estimated = BinomialCoefficient(secretHashes.Count, threshold);
        if (estimated > maxCombinations)
            throw new InvalidOperationException(
                $"Seeder threshold combination count C({secretHashes.Count},{threshold}) = {estimated} exceeds maximum of {maxCombinations}. Reduce the number of seeders or increase the threshold.");

        var result = new List<List<string>>();
        
        result.AddRange(GetCombinations(secretHashes, threshold));

        return result;
    }

    private static long BinomialCoefficient(int n, int k)
    {
        if (k > n) return 0;
        if (k == 0 || k == n) return 1;
        if (k > n - k) k = n - k;
        long result = 1;
        for (int i = 0; i < k; i++)
        {
            result = result * (n - i) / (i + 1);
            if (result > 10_000_000) return result; // early exit to avoid overflow
        }
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
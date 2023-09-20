using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;
using System.Collections.Generic;
using Angor.Shared.Models;

namespace Angor.Shared;

public class ScriptBuilder
{
    public static Script GetAngorFeeOutputScript(string angorKey)
    {
        // ugly hack
        DerivationOperations derivation = new DerivationOperations(null, null, null);

        return derivation.AngorKeyToScript(angorKey);
    }

    public static Script GetSeederInfoScript(string investorKey, string secretHash)
    {
        if (string.IsNullOrEmpty(secretHash))
        {
            return new Script(OpcodeType.OP_RETURN,
                Op.GetPushOp(new PubKey(investorKey).ToBytes()));
        }

        return new Script(OpcodeType.OP_RETURN,
            Op.GetPushOp(new PubKey(investorKey).ToBytes()),
            Op.GetPushOp(uint256.Parse(secretHash).ToBytes()));
    }
    
    public static Script GetProjectStartScript(string founderKey)
    {
        return new Script(OpcodeType.OP_RETURN,
            Op.GetPushOp(new PubKey(founderKey).ToBytes()));
    }

    public static (PubKey investorKey, uint256? secretHash) GetInvestmentDataFromOpReturnScript(Script script)
    {
        if (!script.IsUnspendable)
        {
            throw new Exception();
        }

        var ops = script.ToOps();

        if (ops.Count == 2)
        {
            return (new PubKey(ops[1].PushData), null);
        }

        PubKey pubKey = new PubKey(ops[1].PushData);
        uint256 secretHash = new uint256(ops[2].PushData);

        return (pubKey, secretHash);
    }

    public static Script GetInvestorPenaltyTransactionScript(string investorKey, DateTime punishmentLockTime)
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

    public static ProjectScripts BuildScripts(string funderKey, string funderRecoveryKey, string investorKey, string? secretHash, DateTime founderLockTime, DateTime projectExpieryLocktime, ProjectSeeders seeders)
    {
        long locktimeFounder = Utils.DateTimeToUnixTime(founderLockTime);
        long locktimeExpiery = Utils.DateTimeToUnixTime(projectExpieryLocktime);

        ProjectScripts projectScripts = new();

        // funder gets funds after stage started
        projectScripts.Founder = new Script(new List<Op>
        {
            Op.GetPushOp(new NBitcoin.PubKey(funderKey).GetTaprootFullPubKey().ToBytes()),
            OpcodeType.OP_CHECKSIGVERIFY,
            Op.GetPushOp(locktimeFounder),
            OpcodeType.OP_CHECKLOCKTIMEVERIFY
        });

        if (string.IsNullOrEmpty(secretHash))
        {
            // regular investor pre-co-sign with founder to gets funds with penalty
            projectScripts.Recover = new Script(new List<Op>
            {
                Op.GetPushOp(new NBitcoin.PubKey(funderRecoveryKey).GetTaprootFullPubKey().ToBytes()),
                OpcodeType.OP_CHECKSIGVERIFY,
                Op.GetPushOp(new NBitcoin.PubKey(investorKey).GetTaprootFullPubKey().ToBytes()),
                OpcodeType.OP_CHECKSIG
            });
        }
        else
        {
            //  seed investor pre-co-sign with founder to gets funds with penalty and must expose the secret
            projectScripts.Recover = new Script(new List<Op>
            {
                Op.GetPushOp(new NBitcoin.PubKey(funderRecoveryKey).GetTaprootFullPubKey().ToBytes()),
                OpcodeType.OP_CHECKSIGVERIFY,
                Op.GetPushOp(new NBitcoin.PubKey(investorKey).GetTaprootFullPubKey().ToBytes()),
                OpcodeType.OP_CHECKSIGVERIFY,
                OpcodeType.OP_HASH256,
                Op.GetPushOp(new uint256(secretHash).ToBytes()),
                OpcodeType.OP_EQUAL
            });
        }

        // project ended and investor can collect remaining funds
        projectScripts.EndOfProject = new Script(new List<Op>
        {
            Op.GetPushOp(new NBitcoin.PubKey(investorKey).GetTaprootFullPubKey().ToBytes()),
            OpcodeType.OP_CHECKSIGVERIFY,
            Op.GetPushOp(locktimeExpiery),
            OpcodeType.OP_CHECKLOCKTIMEVERIFY
        });

        if (string.IsNullOrEmpty(secretHash))
        {
            if (seeders.SecretHashes.Any())
            {
                // all the combinations of penalty free recovery based on a threshold of seeder secret hashes
                var seederHashes = BuildSeederScriptTree(investorKey, seeders);

                projectScripts.Seeders.AddRange(seederHashes);
            }
        }

        return projectScripts;
    }

    public static List<Script> BuildSeederScriptTree(string investorKey, ProjectSeeders seeders)
    {
        List<Script> list = new();

        var thresholds = CreateThresholds(seeders.Threshold, seeders.SecretHashes);

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
using System.Text;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using NBitcoin;
using NBitcoin.Crypto;

namespace Angor.Shared.Protocol.Scripts;

public class TaprootScriptBuilder : ITaprootScriptBuilder
{
    public Script CreateStage(AngorNetwork network, ProjectScripts scripts)
    {
        var treeInfo = BuildTaprootSpendInfo(scripts);

        // Bypass treeInfo.OutputPubKey which is computed by NBitcoin's buggy
        // ComputeTapTweak on .NET 10 ARM64. Recompute the output key ourselves.
        var outputKeyBytes = TaprootKeyHelper.GetTaprootOutputKeyBytes(treeInfo.InternalPubKey, treeInfo.MerkleRoot);
        var taprootPubKey = new TaprootPubKey(outputKeyBytes);
        var address = taprootPubKey.GetAddress(network.BitcoinNetwork);
        var scriptBytes = address.ScriptPubKey.ToBytes();

        return new Script(scriptBytes);
    }

    public Script CreateControlBlock(ProjectScripts scripts, Func<ProjectScripts, Script> scriptSelector)
    {
        var treeInfo = BuildTaprootSpendInfo(scripts);

        var script = scriptSelector(scripts);

        ControlBlock controlBlock = treeInfo.GetControlBlock(script.ToTapScript(TapLeafVersion.C0));

        return new Script(controlBlock.ToBytes());
    }

    public (Script controlBlock, Script execute, Script[] secrets) CreateControlSeederSecrets(ProjectScripts scripts, int threshold, Key[] secrets)
    {
        var treeInfo = BuildTaprootSpendInfo(scripts);

        var scriptWeights = BuildTaprootScripts(scripts).Skip(3).Select(s => s.Item2);

        // find the spending script for the current secret hash combination

        var hashesOfSecrets = secrets.Select(secret => (Hashes.DoubleSHA256(secret.ToBytes()), new Script(secret.ToBytes()))).ToList();

        Script execute = null;
        List<Script> secretHashes = new List<Script>();

        foreach (var scriptWeight in scriptWeights)
        {
            var ops = scriptWeight.ToOps().ToList();

            secretHashes.Clear();

            foreach (var op in ops)
            {
                if (op.PushData != null && op.PushData.Length == 32)
                {
                    var comp = new uint256(op.PushData);

                    foreach (var hash in hashesOfSecrets)
                    {
                        if (hash.Item1 == comp)
                        {
                            secretHashes.Add(hash.Item2);
                        }
                    }
                }
            }

            if (secretHashes.Count == threshold)
            {
                execute = scriptWeight;

                break;
            }
        }

        if (execute == null)
        {
            throw new Exception("no secret found that matches the given scripts");
        }

        ControlBlock controlBlock = treeInfo.GetControlBlock(execute.ToTapScript(TapLeafVersion.C0));

        return (new Script(controlBlock.ToBytes()), execute, secretHashes.ToArray());
    }
    
    private static TaprootSpendInfo BuildTaprootSpendInfo(ProjectScripts scripts)
    {
        var taprootKey = CreateUnspendableInternalKey();

        var scriptWeights = BuildTaprootScripts(scripts);

        // Transform the scripts to TapScript format
        var tapScriptWeights = scriptWeights
            .Select(sw => (sw.Item1, sw.Item2.ToTapScript(TapLeafVersion.C0)))
            .ToList();

        var treeInfo = TaprootSpendInfo.WithHuffmanTree(taprootKey, tapScriptWeights.ToArray());

        return treeInfo;
    }

    private static List<(uint, Script)> BuildTaprootScripts(ProjectScripts scripts)
    {
        var scriptWeights = new List<(uint, Script)>()
        {
            (70u, scripts.Founder),
            (40u, scripts.Recover),
            (1u, scripts.EndOfProject)
        };

        foreach (var scriptsSeeder in scripts.Seeders)
        {
            scriptWeights.Add((10u, scriptsSeeder));
        }

        return scriptWeights;
    }

    public static TaprootInternalPubKey CreateUnspendableInternalKey()
    {
        // todo: double check this key is unspendable
        // https://github.com/bitcoin/bips/blob/master/bip-0341.mediawiki#constructing-and-spending-taproot-outputs
        // this is a key that can not be spent, we will always spend a tapscript using scripts
        //var taprootKey = TaprootInternalPubKey.Parse("0x50929b74c1a04954b78b4b6035e97a5e078a5a0f28ec96d547bfee9ace803ac0");

        // 1. Calculate the SHA256 of a known constant
        var sha256 = Hashes.SHA256(Encoding.UTF8.GetBytes("Angor Unspendable Taproot Key"));

        if (!TaprootPubKey.TryCreate(sha256, out TaprootPubKey? taprootPubKey))
        {
            throw new Exception();
        }

        var taprootInternalPubKey = new TaprootInternalPubKey(taprootPubKey.ToBytes());

        return taprootInternalPubKey;
    }
}

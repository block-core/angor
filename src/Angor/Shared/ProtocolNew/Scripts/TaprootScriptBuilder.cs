using System.Linq.Expressions;
using System.Text;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Blockcore.NBitcoin;
using NBitcoin;
using NBitcoin.Crypto;
using Script = Blockcore.Consensus.ScriptInfo.Script;

namespace Angor.Shared.ProtocolNew.Scripts;

public class TaprootScriptBuilder : ITaprootScriptBuilder
{
    public Script CreateStage(Blockcore.Networks.Network network, ProjectScripts scripts)
    {
        var treeInfo = BuildTaprootSpendInfo(scripts);

        var address = treeInfo.OutputPubKey.GetAddress(NetworkMapper.Map(network));

        return new Script(address.ScriptPubKey.ToBytes());
    }

    public Script CreateControlBlock(ProjectScripts scripts, Expression<Func<ProjectScripts,Script>> scriptSelector)
    {
        var treeInfo = BuildTaprootSpendInfo(scripts);

        var script = scriptSelector.Compile().Invoke(scripts);
            
        ControlBlock controlBlock = treeInfo.GetControlBlock(new NBitcoin.Script(script.ToBytes()),
            (byte)TaprootConstants.TAPROOT_LEAF_TAPSCRIPT);

        return new Script(controlBlock.ToBytes());
    }

    public (Script controlBlock, Script execute, Script[] secrets) CreateControlSeederSecrets(ProjectScripts scripts, int threshold, Blockcore.NBitcoin.Key[] secrets)
    {
        var treeInfo = BuildTaprootSpendInfo(scripts);

        var scriptWeights = BuildTaprootScripts(scripts).Skip(3).Select(s => new Script(s.Item2.ToBytes()));

        // find the spending script for the current secret hash combination

        var hashesOfSecrets = secrets.Select(secret => (Blockcore.NBitcoin.Crypto.Hashes.Hash256(secret.ToBytes()), new Script(secret.ToBytes()))).ToList();

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
                    var comp = new Blockcore.NBitcoin.uint256(op.PushData);

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

        ControlBlock controlBlock = treeInfo.GetControlBlock(new NBitcoin.Script(execute.ToBytes()),
            (byte)TaprootConstants.TAPROOT_LEAF_TAPSCRIPT);

        return (new Script(controlBlock.ToBytes()), execute, secretHashes.ToArray());
    }
    
    private static TaprootSpendInfo BuildTaprootSpendInfo(ProjectScripts scripts)
    {
        var taprootKey = CreateUnspendableInternalKey();

        var scriptWeights = BuildTaprootScripts(scripts);

        var treeInfo = TaprootSpendInfo.WithHuffmanTree(taprootKey, scriptWeights.ToArray());

        return treeInfo;
    }

    private static List<(uint, NBitcoin.Script)> BuildTaprootScripts(ProjectScripts scripts)
    {
        var scriptWeights = new List<(uint, NBitcoin.Script)>()
        {
            (70u, new NBitcoin.Script (scripts.Founder.ToBytes())),
            (40u, new NBitcoin.Script (scripts.Recover.ToBytes())),
            (1u, new NBitcoin.Script (scripts.EndOfProject.ToBytes()))
        };

        foreach (var scriptsSeeder in scripts.Seeders)
        {
            scriptWeights.Add((10u, new NBitcoin.Script(scriptsSeeder.ToBytes())));
        }

        return scriptWeights;
    }

    public static TaprootInternalPubKey CreateUnspendableInternalKey()
    {
        // 1. Calculate the SHA256 of a known constant
        var sha256 = Hashes.SHA256(Encoding.UTF8.GetBytes("Angor Unspendable Taproot Key"));

        if (!TaprootPubKey.TryCreate(sha256, out TaprootPubKey? taprootPubKey))
        {
            throw new Exception();
        }

        var taprootInternalPubKey = new TaprootInternalPubKey(taprootPubKey.ToBytes());

        //// todo: double check this key is unspendable
        //https://github.com/bitcoin/bips/blob/master/bip-0341.mediawiki#constructing-and-spending-taproot-outputs
        //// this is a key that can not be spent, we will always spend a tapscript using scripts
        //var taprootKey = TaprootInternalPubKey.Parse("0x50929b74c1a04954b78b4b6035e97a5e078a5a0f28ec96d547bfee9ace803ac0");


        return taprootInternalPubKey;
    }
}
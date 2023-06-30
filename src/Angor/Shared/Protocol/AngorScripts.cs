using Blockcore.NBitcoin;
using NBitcoin;
using PubKey = Blockcore.NBitcoin.PubKey;
using Script = Blockcore.Consensus.ScriptInfo.Script;
using uint256 = Blockcore.NBitcoin.uint256;
using Network = Blockcore.Networks.Network; 

namespace Angor.Shared.Protocol
{
    public class AngorScripts
    {
        public static Script CreateStageSeeder(Network network, byte[] taprootKey, Script founder, Script recover, Script expiry)
        {
            var builder = new TaprootBuilder();

            builder.AddLeaf(1, new NBitcoin.Script(founder.ToBytes()))
                   .AddLeaf(2, new NBitcoin.Script(recover.ToBytes()))
                   .AddLeaf(2, new NBitcoin.Script(expiry.ToBytes()));

            var treeInfo = builder.Finalize(new TaprootInternalPubKey(taprootKey));

            var address = treeInfo.OutputPubKey.GetAddress(NBitcoin.Network.TestNet); //TODO 

            return new Script(address.ScriptPubKey.ToBytes());
        }
        
        public static uint256 CreateStage(List<Script> leaves)
        {

            var builder = new TaprootBuilder();

            uint depth = 1;
            foreach (var script in leaves)
            {
                builder.AddLeaf(depth, new NBitcoin.Script(script.ToBytes()));

                depth += depth % 2 == 0 ? (uint)0 : 1;
            }

            var key = new PubKey(string.Empty);

            var merkelRoot = builder.Finalize(new TaprootInternalPubKey(key.ToBytes())).MerkleRoot;
            
            return new uint256(merkelRoot.ToBytes());
        }
    }
}
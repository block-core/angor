using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using NBitcoin;
using PubKey = Blockcore.NBitcoin.PubKey;
using Script = Blockcore.Consensus.ScriptInfo.Script;
using uint256 = Blockcore.NBitcoin.uint256;

namespace Angor.Shared.Protocol
{
    public class AngorScripts
    {
        public static Script CreateStageSeeder(Network network, TaprootInternalPubKey taprootKey, Script founder, Script recover, Script expiry)
        {
            var builder = new TaprootBuilder();

            builder.AddLeaf(1, founder)
                   .AddLeaf(2, recover)
                   .AddLeaf(2, expiry);

            var treeInfo = builder.Finalize(taprootKey);

            var address = treeInfo.OutputPubKey.GetAddress(network);

            return address.ScriptPubKey;
        }
        
        public static uint256 CreateStage(List<Script> leaves)
        {

            var builder = new TaprootBuilder();

            uint depth = 1;
            foreach (var script in leaves)
            {
                builder.AddLeaf(depth, new NBitcoin.Script(script.ToBytes()));

                depth += depth % 2 == 0 ? (uint)1 : 0;
            }

            var key = new PubKey(string.Empty);

            var merkelRoot = builder.Finalize(new TaprootInternalPubKey(key.ToBytes())).MerkleRoot;
            
            return new uint256(merkelRoot.ToBytes());
        }
    }
}
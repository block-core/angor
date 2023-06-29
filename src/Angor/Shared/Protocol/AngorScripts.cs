using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using NBitcoin;
using Script = NBitcoin.Script;

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
    }
}
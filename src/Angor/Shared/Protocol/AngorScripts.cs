using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using NBitcoin;
using Script = NBitcoin.Script;

namespace Angor.Shared.Protocol
{
    public class AngorScripts
    {
        public static Script CreateStage(List<Script> leaves)
        {

            var builder = new TaprootBuilder();

            foreach (var script in leaves)
            {
                //builder.
            }

            return null;
        }
    }
}
using System.Text;
using NBitcoin;
using NBitcoin.Crypto;
using PubKey = Blockcore.NBitcoin.PubKey;
using Script = Blockcore.Consensus.ScriptInfo.Script;
using uint256 = Blockcore.NBitcoin.uint256;


namespace Angor.Shared.Protocol
{
    public class AngorScripts
    {
        public static Script CreateStageSeeder(Blockcore.Networks.Network network, Script founder, Script recover, Script expiry)
        {
            TaprootInternalPubKey taprootKey = CreateUnspendableInternalKey();

            var builder = new TaprootBuilder();

            builder.AddLeaf(1, new NBitcoin.Script(founder.ToBytes()))
                   .AddLeaf(2, new NBitcoin.Script(recover.ToBytes()))
                   .AddLeaf(2, new NBitcoin.Script(expiry.ToBytes()));

            var treeInfo = builder.Finalize(taprootKey);

            var address = treeInfo.OutputPubKey.GetAddress(NBitcoin.Network.TestNet); //TODO 

            return new Script(address.ScriptPubKey.ToBytes());
        }

        public static Script CreateControlBlockFounder(Blockcore.Networks.Network network, Script founder, Script recover, Script expiry)
        {
            TaprootInternalPubKey taprootKey = CreateUnspendableInternalKey();

            var builder = new TaprootBuilder();

            builder.AddLeaf(1, new NBitcoin.Script(founder.ToBytes()))
                .AddLeaf(2, new NBitcoin.Script(recover.ToBytes()))
                .AddLeaf(2, new NBitcoin.Script(expiry.ToBytes()));

            var treeInfo = builder.Finalize(taprootKey);

            ControlBlock controlBlock = treeInfo.GetControlBlock(new NBitcoin.Script(founder.ToBytes()), (byte)NBitcoin.TaprootConstants.TAPROOT_LEAF_TAPSCRIPT);

            return new Script(controlBlock.ToBytes());
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
}
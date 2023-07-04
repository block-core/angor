using System.Text;
using NBitcoin;
using NBitcoin.Crypto;
using Script = Blockcore.Consensus.ScriptInfo.Script;


namespace Angor.Shared.Protocol
{
    public class AngorScripts
    {
        public static Script CreateStageSeeder(Blockcore.Networks.Network network, Script founder, Script recover, Script expiry)
        {
            var taprootKey = CreateUnspendableInternalKey();

            var builder = new TaprootBuilder();

            builder.AddLeaf(1, new NBitcoin.Script(founder.ToBytes()))
                   .AddLeaf(2, new NBitcoin.Script(recover.ToBytes()))
                   .AddLeaf(2, new NBitcoin.Script(expiry.ToBytes()));

            var treeInfo = builder.Finalize(taprootKey);

            var address = treeInfo.OutputPubKey.GetAddress(NetworkMapper.Map(network));

            return new Script(address.ScriptPubKey.ToBytes());
        }

        public static Script CreateControlBlockFounder(Script founder, Script recover, Script expiry)
        {
            var taprootKey = CreateUnspendableInternalKey();

            var builder = new TaprootBuilder();

            builder.AddLeaf(1, new NBitcoin.Script(founder.ToBytes()))
                .AddLeaf(2, new NBitcoin.Script(recover.ToBytes()))
                .AddLeaf(2, new NBitcoin.Script(expiry.ToBytes()));

            var treeInfo = builder.Finalize(taprootKey);

            ControlBlock controlBlock = treeInfo.GetControlBlock(new NBitcoin.Script(founder.ToBytes()), 
                (byte)TaprootConstants.TAPROOT_LEAF_TAPSCRIPT);

            return new Script(controlBlock.ToBytes());
        }

        public static Script CreateControlBlockExpiry(Script founder, Script recover, Script expiry)
        {
            var taprootKey = CreateUnspendableInternalKey();

            var builder = new TaprootBuilder();

            builder.AddLeaf(1, new NBitcoin.Script(founder.ToBytes()))
                .AddLeaf(2, new NBitcoin.Script(recover.ToBytes()))
                .AddLeaf(2, new NBitcoin.Script(expiry.ToBytes()));

            var treeInfo = builder.Finalize(taprootKey);

            ControlBlock controlBlock = treeInfo.GetControlBlock(new NBitcoin.Script(expiry.ToBytes()),
                (byte)TaprootConstants.TAPROOT_LEAF_TAPSCRIPT);

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
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;

namespace Angor.Shared;

public class ScriptBuilder
{

    public static Script GetAngorFeeOutputScript(string angorKey)
    {
        return new PubKey(angorKey).WitHash.ScriptPubKey;

        //return new Script(
        //    OpcodeType.OP_DUP,
        //    OpcodeType.OP_HASH160,
        //    Op.GetPushOp(new PubKey(angorKey).ToBytes()),
        //    OpcodeType.OP_EQUALVERIFY,
        //    OpcodeType.OP_CHECKSIG
        //);
    }

    public static Script GetSeederInfoScript(string investorKey, string secretHash)
    {
        return new Script(OpcodeType.OP_RETURN,
            Op.GetPushOp(new PubKey(investorKey).ToBytes()),
            Op.GetPushOp(uint256.Parse(secretHash).ToBytes()));
    }
    
    public static (Script founder,Script recover, Script endOfProject) BuildSeederScript(string funderKey, string investorKey, string secretHash, DateTime founderLockTime, DateTime projectExpieryLocktime)
    {
        long locktimeFounder = Utils.DateTimeToUnixTime(founderLockTime);
        long locktimeExpiery = Utils.DateTimeToUnixTime(projectExpieryLocktime);

        return (

            // funder gets funds after stage started
            new(new List<Op>
            {
                Op.GetPushOp(new NBitcoin.PubKey(funderKey).GetTaprootFullPubKey().ToBytes()),
                OpcodeType.OP_CHECKSIGVERIFY,
                Op.GetPushOp(locktimeFounder),
                OpcodeType.OP_CHECKLOCKTIMEVERIFY
            }),

            // seed investor pre-co-sign with founder to gets funds with penalty and must expose the secret
            new(new List<Op>
            {
                Op.GetPushOp(new NBitcoin.PubKey(funderKey).GetTaprootFullPubKey().ToBytes()),
                OpcodeType.OP_CHECKSIGVERIFY,
                Op.GetPushOp(new NBitcoin.PubKey(investorKey).GetTaprootFullPubKey().ToBytes()),
                OpcodeType.OP_CHECKSIGVERIFY,
                OpcodeType.OP_SHA256,
                Op.GetPushOp(new uint256(secretHash).ToBytes()),
                OpcodeType.OP_EQUALVERIFY
            }),

            // project ended and investor can collect remaining funds
            new(new List<Op>
            {
                Op.GetPushOp(new NBitcoin.PubKey(investorKey).GetTaprootFullPubKey().ToBytes()),
                OpcodeType.OP_CHECKSIGVERIFY,
                Op.GetPushOp(locktimeExpiery),
                OpcodeType.OP_CHECKLOCKTIMEVERIFY
            })
        );
    }
}
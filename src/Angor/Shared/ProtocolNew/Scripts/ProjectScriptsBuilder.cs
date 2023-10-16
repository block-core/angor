using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;

namespace Angor.Shared.ProtocolNew.Scripts;

public class ProjectScriptsBuilder : IProjectScriptsBuilder
{
    private readonly IDerivationOperations _derivationOperations;

    public ProjectScriptsBuilder(IDerivationOperations derivationOperations)
    {
        _derivationOperations = derivationOperations;
    }

    public Script GetAngorFeeOutputScript(string angorKey)
    {
        return _derivationOperations.AngorKeyToScript(angorKey);
    }

    public Script BuildInvestorInfoScript(string investorKey)
    {
        return new Script(OpcodeType.OP_RETURN,
            Op.GetPushOp(new PubKey(investorKey).ToBytes()));
    }

    public Script BuildFounderInfoScript(string founderKey, string nostrPuKey)
    {
        return new Script(OpcodeType.OP_RETURN,
            Op.GetPushOp(new PubKey(founderKey).ToBytes()),
            Op.GetPushOp(new NBitcoin.PubKey(nostrPuKey).GetTaprootFullPubKey().ToBytes()));
    }

    public Script BuildSeederInfoScript(string investorKey, uint256 secretHash)
    {
        return new Script(OpcodeType.OP_RETURN,
            Op.GetPushOp(new PubKey(investorKey).ToBytes()),
            Op.GetPushOp(secretHash.ToBytes()));
    }

    public (string investorKey, uint256? secretHash) GetInvestmentDataFromOpReturnScript(Script script)
    {
        if (!script.IsUnspendable)
        {
            throw new Exception();
        }

        var ops = script.ToOps();

        if (ops.Count == 2)
        {
            return (new PubKey(ops[1].PushData).ToHex(), null);
        }

        PubKey pubKey = new PubKey(ops[1].PushData);
        uint256 secretHash = new uint256(ops[2].PushData);

        return (pubKey.ToHex(), secretHash);
    }
}
using NBitcoin;

namespace Angor.Shared.Protocol.Scripts;

public interface ISeederScriptTreeBuilder
{
    IEnumerable<Script> BuildSeederScriptTree(string investorKey, int seederThreshold, List<string> secretHashes);
}
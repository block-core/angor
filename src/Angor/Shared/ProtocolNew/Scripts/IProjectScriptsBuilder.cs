using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;

namespace Angor.Shared.ProtocolNew.Scripts;

public interface IProjectScriptsBuilder
{
    Script GetAngorFeeOutputScript(string angorKey);
    Script BuildInvestorInfoScript(string investorKey, string secretHash);
    
    Script BuildSeederInfoScript(string investorKey, string secretHash);
    
    (PubKey investorKey, uint256? secretHash) GetInvestmentDataFromOpReturnScript(Script script);
}
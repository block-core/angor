using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;

namespace Angor.Shared.Protocol.Scripts;

public interface IProjectScriptsBuilder
{
    Script GetAngorFeeOutputScript(string angorKey);
    Script BuildInvestorInfoScript(string investorKey);
    Script BuildFounderInfoScript(string founderKey, short keyType, string nostrEventId);
    
    Script BuildSeederInfoScript(string investorKey, uint256 secretHash);
    
    (string investorKey, uint256? secretHash) GetInvestmentDataFromOpReturnScript(Script script);
}
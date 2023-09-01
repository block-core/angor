using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.ProtocolNew.Scripts;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using TxIn = Blockcore.Consensus.TransactionInfo.TxIn;

namespace Angor.Shared.ProtocolNew.TransactionBuilders;

public class InvestmentTransactionBuilder : IInvestmentTransactionBuilder
{
    private readonly INetworkConfiguration _networkConfiguration;
    private readonly IProjectScriptsBuilder _projectScriptsBuilder;
    private readonly IInvestmentScriptBuilder _investmentScriptBuilder;

    public InvestmentTransactionBuilder(INetworkConfiguration networkConfiguration, IProjectScriptsBuilder projectScriptsBuilder, 
        IInvestmentScriptBuilder investmentScriptBuilder)
    {
        _networkConfiguration = networkConfiguration;
        _projectScriptsBuilder = projectScriptsBuilder;
        _investmentScriptBuilder = investmentScriptBuilder;
    }

    public Transaction BuildInvestmentTransaction(ProjectInfo projectInfo, Script opReturnScript, 
        IEnumerable<ProjectScripts> projectScripts, long totalInvestmentAmount)
    {
        var network = _networkConfiguration.GetNetwork();

        Transaction investmentTransaction = network.Consensus.ConsensusFactory.CreateTransaction();

        // create the output and script of the project id 
        var angorFeeOutputScript = _projectScriptsBuilder.GetAngorFeeOutputScript(projectInfo.ProjectIdentifier);
        var angorOutput = new TxOut(new Money(totalInvestmentAmount / 100), angorFeeOutputScript);
        investmentTransaction.AddOutput(angorOutput);
        
        var investorInfoOutput = new TxOut(new Money(0), opReturnScript);
        investmentTransaction.AddOutput(investorInfoOutput);

        var stagesScripts = projectScripts.Select(_ => AngorScripts.CreateStage(network, _));

        var stagesOutputs = stagesScripts.Select((_, i) =>
            new TxOut(new Money(Convert.ToInt64(totalInvestmentAmount * projectInfo.Stages[i].AmountToRelease)),
                new Script(_.ToBytes())));

        investmentTransaction.Outputs.AddRange(stagesOutputs);

        return investmentTransaction;
    }
    
    public IEnumerable<Transaction> BuildUpfrontRecoverFundsTransactions(Transaction investmentTransaction, DateTime penaltyDate, string investorReceiveAddress)
    {
        var network = _networkConfiguration.GetNetwork();
        // allow an investor that acquired enough seeder secrets to recover their investment
        var nbitcoinNetwork = NetworkMapper.Map(network);
        var nbitcoinInvestmentTrx = NBitcoin.Transaction.Parse(investmentTransaction.ToHex(), nbitcoinNetwork);

        var spendingScript = _investmentScriptBuilder.GetInvestorPenaltyTransactionScript(
            investorReceiveAddress,
            penaltyDate);
        
        return nbitcoinInvestmentTrx.Outputs.AsIndexedOutputs()
            .Where(_ => _.TxOut.ScriptPubKey.IsScriptType(NBitcoin.ScriptType.Taproot))
            .Select((_, i) =>
            {
                var stageTransaction = nbitcoinNetwork.CreateTransaction();
                
                stageTransaction.Inputs.Add(new NBitcoin.OutPoint(_.Transaction, _.N));
                
                stageTransaction.Outputs.Add(new NBitcoin.TxOut(_.TxOut.Value,
                    new NBitcoin.Script(spendingScript.WitHash.ScriptPubKey.ToBytes())));

                return network.Consensus.ConsensusFactory.CreateTransaction(stageTransaction.ToHex());;
            });
    }

    public Transaction BuildUpfrontRecoverFundsTransaction(Transaction investmentTransaction, DateTime penaltyDate,
        string investorReceiveAddress)
    {
        var spendingScript = _investmentScriptBuilder.GetInvestorPenaltyTransactionScript(
            investorReceiveAddress,
            penaltyDate);

        var transaction = _networkConfiguration.GetNetwork().CreateTransaction();

        foreach (var output in investmentTransaction.Outputs.AsIndexedOutputs().Where(_ => _.N > 1))
        {
            transaction.Inputs.Add( new TxIn(output.ToOutPoint()));

            transaction.Outputs.Add(new TxOut(output.TxOut.Value, spendingScript.WitHash.ScriptPubKey));
        }

        return transaction;
    }
}
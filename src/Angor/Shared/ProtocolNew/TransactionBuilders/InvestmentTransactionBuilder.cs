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
            new TxOut(new Money(Convert.ToInt64(totalInvestmentAmount * (projectInfo.Stages[i].AmountToRelease / 100))),
                new Script(_.ToBytes())));

        investmentTransaction.Outputs.AddRange(stagesOutputs);

        return investmentTransaction;
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
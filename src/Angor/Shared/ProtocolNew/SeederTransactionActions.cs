using Angor.Shared.Protocol;
using Angor.Shared.ProtocolNew.Scripts;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;

namespace Angor.Shared.ProtocolNew;

public class SeederTransactionActions : ISeederTransactionActions
{
    private readonly INetworkConfiguration _networkConfiguration;
    private readonly IInvestmentScriptBuilder _investmentScriptBuilder;
    private readonly IProjectScriptsBuilder _projectScriptsBuilder;

    public SeederTransactionActions(INetworkConfiguration networkConfiguration, IInvestmentScriptBuilder investmentScriptBuilder, 
        IProjectScriptsBuilder projectScriptsBuilder)
    {
        _networkConfiguration = networkConfiguration;
        _investmentScriptBuilder = investmentScriptBuilder;
        _projectScriptsBuilder = projectScriptsBuilder;
    }

    public Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, string investorKey,
        string investorSecretHash, long totalInvestmentAmount)
    {
        var network = _networkConfiguration.GetNetwork();

        Transaction investmentTransaction = network.Consensus.ConsensusFactory.CreateTransaction();

        // create the output and script of the project id 
        var angorFeeOutputScript = _projectScriptsBuilder.GetAngorFeeOutputScript(projectInfo.ProjectIdentifier);
        var angorOutput = new TxOut(new Money(totalInvestmentAmount / 100), angorFeeOutputScript);
        investmentTransaction.AddOutput(angorOutput);

        // create the output and script of the investor pubkey script opreturn
        var opreturnScript = _projectScriptsBuilder.BuildSeederInfoScript(investorKey, investorSecretHash);
        var investorInfoOutput = new TxOut(new Money(0), opreturnScript);
        investmentTransaction.AddOutput(investorInfoOutput);

        // stages, this is an iteration over the stages to create the taproot spending script branches for each stage
        var stagesScript = projectInfo.Stages
            .Select(_ =>
                _investmentScriptBuilder.BuildSSeederScripts(projectInfo.FounderKey,
                    investorKey,
                    investorSecretHash,
                    _.ReleaseDate,
                    projectInfo.ExpiryDate));

        var stagesScripts = stagesScript.Select(_ => AngorScripts.CreateStage(network, _));

        var stagesOutputs = stagesScripts.Select((_, i) =>
            new TxOut(new Money(Convert.ToInt64(totalInvestmentAmount * projectInfo.Stages[i].AmountToRelease)),
                new Script(_.ToBytes())));

        foreach (var stagesOutput in stagesOutputs)
        {
            investmentTransaction.AddOutput(stagesOutput);
        }

        return investmentTransaction;
    }

    public IEnumerable<Transaction> BuildRecoverSeederFundsTransactions(Transaction investmentTransaction, DateTime penaltyDate, string investorReceiveAddress)
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
}
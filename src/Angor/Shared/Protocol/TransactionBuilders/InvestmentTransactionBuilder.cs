using Angor.Shared.Models;
using Angor.Shared.Protocol.Scripts;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using TxIn = Blockcore.Consensus.TransactionInfo.TxIn;

namespace Angor.Shared.Protocol.TransactionBuilders;

public class InvestmentTransactionBuilder : IInvestmentTransactionBuilder
{
    private readonly INetworkConfiguration _networkConfiguration;
    private readonly IProjectScriptsBuilder _projectScriptsBuilder;
    private readonly IInvestmentScriptBuilder _investmentScriptBuilder;
    private readonly ITaprootScriptBuilder _taprootScriptBuilder;

    public InvestmentTransactionBuilder(INetworkConfiguration networkConfiguration, IProjectScriptsBuilder projectScriptsBuilder, 
        IInvestmentScriptBuilder investmentScriptBuilder, ITaprootScriptBuilder taprootScriptBuilder)
    {
        _networkConfiguration = networkConfiguration;
        _projectScriptsBuilder = projectScriptsBuilder;
        _investmentScriptBuilder = investmentScriptBuilder;
        _taprootScriptBuilder = taprootScriptBuilder;
    }

    public Transaction BuildInvestmentTransaction(ProjectInfo projectInfo, Script opReturnScript, 
        IEnumerable<ProjectScripts> projectScripts, long totalInvestmentAmount)
    {
        var network = _networkConfiguration.GetNetwork();

        Transaction investmentTransaction = network.Consensus.ConsensusFactory.CreateTransaction();

        // create the output and script of the project id 
        var angorFeeOutputScript = _projectScriptsBuilder.GetAngorFeeOutputScript(projectInfo.ProjectIdentifier);
        int angorFeePercentage = _networkConfiguration.GetAngorInvestFeePercentage;
        long angorFee = (totalInvestmentAmount * angorFeePercentage) / 100; 
        var angorOutput = new TxOut(new Money(angorFee), angorFeeOutputScript);
        investmentTransaction.AddOutput(angorOutput);

        // reduce the fee from the total investment amount
        var totalInvestmentAmountAfterFee = totalInvestmentAmount - angorFee;

        var investorInfoOutput = new TxOut(new Money(0), opReturnScript);
        investmentTransaction.AddOutput(investorInfoOutput);

        var stagesScripts = projectScripts.Select(_ => _taprootScriptBuilder.CreateStage(network, _));

        // Calculate amounts for each stage
        var stageAmounts = new List<long>();
        long totalAllocated = 0;

        for (int i = 0; i < projectInfo.Stages.Count; i++)
        {
            long stageAmount;
            if (i == projectInfo.Stages.Count - 1) // Last stage gets remainder
            {
                stageAmount = totalInvestmentAmountAfterFee - totalAllocated;
            }
            else
            {
                stageAmount = Convert.ToInt64(totalInvestmentAmountAfterFee * (projectInfo.Stages[i].AmountToRelease / 100));
                totalAllocated += stageAmount;
            }
            stageAmounts.Add(stageAmount);
        }

        var stagesOutputs = stagesScripts.Select((script, i) =>
            new TxOut(new Money(stageAmounts[i]), new Script(script.ToBytes())));

        investmentTransaction.Outputs.AddRange(stagesOutputs);

        if(investmentTransaction.TotalOut.Satoshi != totalInvestmentAmount)
            throw new InvalidOperationException($"Total output amount {investmentTransaction.TotalOut.Satoshi} does not match expected total investment amount {totalInvestmentAmount}.");

        return investmentTransaction;
    }

    public Transaction BuildUpfrontRecoverFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, int penaltyDays, string investorKey)
    {
        var spendingScript = _investmentScriptBuilder.GetInvestorPenaltyTransactionScript(
            investorKey,
            penaltyDays);

        var transaction = _networkConfiguration.GetNetwork().CreateTransaction();
        
        foreach (var output in investmentTransaction.Outputs.AsIndexedOutputs().Skip(2).Take(projectInfo.Stages.Count))
        {
            transaction.Inputs.Add( new TxIn(output.ToOutPoint()));

            transaction.Outputs.Add(new TxOut(output.TxOut.Value, spendingScript.WitHash.ScriptPubKey));
        }

        return transaction;
    }

    public Transaction BuildUpfrontUnfundedReleaseFundsTransaction(ProjectInfo projectInfo, Transaction investmentTransaction, string investorReleaseKey)
    {
        // the release may be an address or a pubkey, first check if it is an address
        Script spendingScript = null;
        if (BitcoinWitPubKeyAddress.IsValid(investorReleaseKey, _networkConfiguration.GetNetwork(), out Exception _))
        {
            spendingScript = new BitcoinWitPubKeyAddress(investorReleaseKey, _networkConfiguration.GetNetwork()).ScriptPubKey;
        }
        else  // if it is not an address, then it is a pubkey
        {
            // for the release we just send to a regular witness address
            spendingScript = new PubKey(investorReleaseKey).WitHash.ScriptPubKey;
        }

        var transaction = _networkConfiguration.GetNetwork().CreateTransaction();

        foreach (var output in investmentTransaction.Outputs.AsIndexedOutputs().Skip(2).Take(projectInfo.Stages.Count))
        {
            transaction.Inputs.Add(new TxIn(output.ToOutPoint()));

            transaction.Outputs.Add(new TxOut(output.TxOut.Value, spendingScript));
        }

        return transaction;
    }
}
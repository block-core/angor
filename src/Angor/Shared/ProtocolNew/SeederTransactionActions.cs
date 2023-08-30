using Angor.Shared.Models;
//using Angor.Shared.Protocol;
using Angor.Shared.ProtocolNew.Scripts;
using Angor.Shared.ProtocolNew.TransactionBuilders;
using NBitcoin;
using Transaction = Blockcore.Consensus.TransactionInfo.Transaction;

namespace Angor.Shared.ProtocolNew;

public class SeederTransactionActions : ISeederTransactionActions
{
    private readonly IInvestmentScriptBuilder _investmentScriptBuilder;
    private readonly IProjectScriptsBuilder _projectScriptsBuilder;
    private readonly ISpendingTransactionBuilder _spendingTransactionBuilder;
    private readonly IInvestmentTransactionBuilder _investmentTransactionBuilder;
    private readonly ITaprootScriptBuilder _taprootScriptBuilder;

    public SeederTransactionActions(IInvestmentScriptBuilder investmentScriptBuilder, IProjectScriptsBuilder projectScriptsBuilder, 
        ISpendingTransactionBuilder spendingTransactionBuilder, IInvestmentTransactionBuilder investmentTransactionBuilder, ITaprootScriptBuilder taprootScriptBuilder)
    {
        _investmentScriptBuilder = investmentScriptBuilder;
        _projectScriptsBuilder = projectScriptsBuilder;
        _spendingTransactionBuilder = spendingTransactionBuilder;
        _investmentTransactionBuilder = investmentTransactionBuilder;
        _taprootScriptBuilder = taprootScriptBuilder;
    }

    public Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, string investorKey,
        string investorSecretHash, long totalInvestmentAmount)
    {
        // create the output and script of the investor pubkey script opreturn
        var opreturnScript = _projectScriptsBuilder.BuildSeederInfoScript(investorKey, investorSecretHash);

        // stages, this is an iteration over the stages to create the taproot spending script branches for each stage
        var stagesScript = projectInfo.Stages
            .Select((_,index) => _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo,
                investorKey,index, investorSecretHash));

        return _investmentTransactionBuilder.BuildInvestmentTransaction(projectInfo, opreturnScript, stagesScript,
            totalInvestmentAmount);
    }
    
    public IEnumerable<Transaction> BuildRecoverSeederFundsTransactions(Transaction investmentTransaction, DateTime penaltyDate, string investorReceiveAddress)
    {
        return _investmentTransactionBuilder.BuildUpfrontRecoverFundsTransactions(investmentTransaction, penaltyDate,
            investorReceiveAddress);
    }

    public Transaction RecoverEndOfProjectFunds(string transactionHex, ProjectInfo projectInfo, int stageIndex, string investorReceiveAddress,
        string investorPrivateKey, FeeEstimation feeEstimation)
    {
        return _spendingTransactionBuilder.BuildRecoverInvestorRemainingFundsInProject(transactionHex, projectInfo, stageIndex,
            investorReceiveAddress, investorPrivateKey, new FeeRate(new NBitcoin.Money(feeEstimation.FeeRate)),
            _ =>
            {
                var controlBlock = _taprootScriptBuilder.CreateControlBlock(_, script => script.EndOfProject);
                var fakeSig = new byte[64];
                return new WitScript(Op.GetPushOp(fakeSig), Op.GetPushOp(_.EndOfProject.ToBytes()),
                    Op.GetPushOp(controlBlock.ToBytes()));
            },
            (witScript, sig) =>
            {
                var scriptToExecute = witScript[1];
                var controlBlock = witScript[2];

                return new WitScript(Op.GetPushOp(sig.ToBytes()), Op.GetPushOp(scriptToExecute), Op.GetPushOp(controlBlock));
            });
    }
}
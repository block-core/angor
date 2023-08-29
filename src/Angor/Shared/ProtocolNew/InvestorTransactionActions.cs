using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.ProtocolNew.Scripts;
using Angor.Shared.ProtocolNew.TransactionBuilders;
using NBitcoin;
using Key = Blockcore.NBitcoin.Key;
using Transaction = Blockcore.Consensus.TransactionInfo.Transaction;

namespace Angor.Shared.ProtocolNew;

public class InvestorTransactionActions : IInvestorTransactionActions
{
    private readonly IInvestmentScriptBuilder _investmentScriptBuilder;
    private readonly IProjectScriptsBuilder _projectScriptsBuilder;
    private readonly ISpendingTransactionBuilder _spendingTransactionBuilder;
    private readonly IInvestmentTransactionBuilder _investmentTransactionBuilder;

    public InvestorTransactionActions(IInvestmentScriptBuilder investmentScriptBuilder, IProjectScriptsBuilder projectScriptsBuilder, ISpendingTransactionBuilder spendingTransactionBuilder, IInvestmentTransactionBuilder investmentTransactionBuilder)
    {
        _investmentScriptBuilder = investmentScriptBuilder;
        _projectScriptsBuilder = projectScriptsBuilder;
        _spendingTransactionBuilder = spendingTransactionBuilder;
        _investmentTransactionBuilder = investmentTransactionBuilder;
    }

    public Transaction CreateInvestmentTransaction(ProjectInfo projectInfo, string investorKey,
        long totalInvestmentAmount)
    {
        // create the output and script of the investor pubkey script opreturn
        var opreturnScript = _projectScriptsBuilder.BuildInvestorInfoScript(investorKey);

        // stages, this is an iteration over the stages to create the taproot spending script branches for each stage
        var stagesScript = projectInfo.Stages
            .Select((_,index) => _investmentScriptBuilder.BuildProjectScriptsForStage(projectInfo, investorKey, index));

        return _investmentTransactionBuilder.BuildInvestmentTransaction(projectInfo, opreturnScript, stagesScript,
            totalInvestmentAmount);
    }

    public IEnumerable<Transaction> BuildRecoverInvestorFundsTransactions(Transaction investmentTransaction, DateTime penaltyDate, string investorReceiveAddress)
    {
        return _investmentTransactionBuilder.BuildUpfrontRecoverFundsTransactions(investmentTransaction, penaltyDate,
            investorReceiveAddress);
    }

    public Transaction RecoverEndOfProjectFunds(string transactionHex, ProjectInfo projectInfo, int stageIndex,
        string investorReceiveAddress, string investorPrivateKey, FeeEstimation feeEstimation)
    {
        return _spendingTransactionBuilder.BuildRecoverInvestorRemainingFundsInProject(transactionHex, projectInfo, stageIndex,
            investorReceiveAddress, investorPrivateKey, new NBitcoin.FeeRate(new NBitcoin.Money(feeEstimation.FeeRate)),
            projectScripts =>
            {
                var controlBlock = AngorScripts.CreateControlBlockExpiry(projectScripts); //TODO replace call to interface
                var fakeSig = new byte[64];
                return new NBitcoin.WitScript(NBitcoin.Op.GetPushOp(fakeSig),
                    NBitcoin.Op.GetPushOp(projectScripts.EndOfProject.ToBytes()),
                    NBitcoin.Op.GetPushOp(controlBlock.ToBytes()));
            },
            (witScript, sig) =>
            {
                var scriptToExecute = witScript[1];
                var controlBlock = witScript[2];

                return new NBitcoin.WitScript(NBitcoin.Op.GetPushOp(sig.ToBytes()),
                    NBitcoin.Op.GetPushOp(scriptToExecute), NBitcoin.Op.GetPushOp(controlBlock));
            });
    }

    public Transaction RecoverRemainingFundsWithPenalty(string transactionHex, ProjectInfo projectInfo, int stageIndex,
        string investorReceiveAddress, string investorPrivateKey, FeeEstimation feeEstimation,
        IEnumerable<byte[]> seederSecrets)
    {
        var secrets = seederSecrets.Select(_ => new Key(_));
        
        return _spendingTransactionBuilder.BuildRecoverInvestorRemainingFundsInProject(transactionHex, projectInfo, stageIndex,
            investorReceiveAddress, investorPrivateKey, new NBitcoin.FeeRate(new NBitcoin.Money(feeEstimation.FeeRate)),
            _ =>
            {
                var result = AngorScripts.CreateControlSeederSecrets(_, secrets.ToArray());

                // use fake data for fee estimation
                var fakeSig = new byte[64];

                List<Op> ops = new List<Op>();

                ops.Add(Op.GetPushOp(fakeSig));

                foreach (var secret in result.secrets.Reverse())
                {
                    ops.Add(Op.GetPushOp(secret.ToBytes()));
                }

                ops.Add(Op.GetPushOp(result.execute.ToBytes()));
                ops.Add(Op.GetPushOp(result.controlBlock.ToBytes()));

                return new NBitcoin.WitScript(ops.ToArray());
            },
            (witScript, sig) =>
            {
                var controBlock = new NBitcoin.Script(witScript[witScript.PushCount - 1]);
                var scriptToExecute = new NBitcoin.Script(witScript[witScript.PushCount - 2]);
                
                List<Op> ops = new List<Op>();

                // the last 3 items on the stack are the fakesig, script and controlblock anything before that is the secrets

                ops.Add(Op.GetPushOp(sig.ToBytes()));

                foreach (var oppush  in witScript.Pushes.Skip(1).Take(witScript.Pushes.Count() - 3))
                {
                    ops.Add(Op.GetPushOp(oppush));
                }

                ops.Add(Op.GetPushOp(scriptToExecute.ToBytes()));
                ops.Add(Op.GetPushOp(controBlock.ToBytes()));

                return new NBitcoin.WitScript(ops.ToArray());
            });
    }
}
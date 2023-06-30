using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.Networks;

namespace Angor.Shared;

public class TestingTransactionBuilder
{
    private Transaction BuildSeederTransaction(Network network, Money amount)
    {
        
        var transactinBuilder = new TransactionBuilder(network);

        // var script = ScriptBuilder.BuildSeederScript()
        //
        // transactinBuilder.AddCoins(new Coin(new uint256(), 0, amount, script));

        return transactinBuilder.BuildTransaction(false);
    }
}
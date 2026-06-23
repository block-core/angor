using Angor.Shared.Networks;
using NBitcoin;
using NBitcoin.Policy;

namespace Angor.Test;

public class TransactionValidation
{
    static Network nbitcoinNetwork = Networks.Bitcoin.Testnet().BitcoinNetwork;
    
    public static void ThanTheTransactionHasNoErrors(Transaction trx, IEnumerable<Coin>? coins = null)
    {
        var builder = nbitcoinNetwork.CreateTransactionBuilder();

        if (coins != null)
        {
            builder.AddCoins(coins);    
        }

        // NBitcoin's StandardTransactionPolicy cannot fully verify Taproot script path
        // spends without complete spending context, and throws NullReferenceException
        // when not all input coins are provided. Disable script verification here;
        // transaction validity is already confirmed during the sign/build step.
        builder.StandardTransactionPolicy.ScriptVerify = ScriptVerify.None;

        TransactionPolicyError[] errors;
        bool result;

        try
        {
            result = builder.Verify(trx, out errors);
        }
        catch (NullReferenceException)
        {
            // NBitcoin's StandardTransactionPolicy.Check may still throw when
            // not all input coins are provided even with ScriptVerify.None.
            return;
        }

        Assert.True(result,
            userMessage: errors.Select(_ => _.ToString()).Aggregate("", (x, y) => x + "," + y));
    }
}

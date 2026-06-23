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
        
        Assert.True(builder.Verify(trx, out TransactionPolicyError[] errors),
            userMessage: errors.Select(_ => _.ToString()).Aggregate("", (x, y) => x + "," + y));
    }
}

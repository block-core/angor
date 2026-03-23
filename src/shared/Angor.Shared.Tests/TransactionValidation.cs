using Angor.Shared;
using Angor.Shared.Networks;
using NBitcoin;
using Coin = Blockcore.NBitcoin.Coin;
using Transaction = Blockcore.Consensus.TransactionInfo.Transaction;

namespace Angor.Test;

public class TransactionValidation
{
    static NBitcoin.Network nbitcoinNetwork = NetworkMapper.Map(Networks.Bitcoin.Testnet());
    
    public static void ThanTheTransactionHasNoErrors(Transaction trx, IEnumerable<Coin>? coins = null)
    {
        var builder = nbitcoinNetwork.CreateTransactionBuilder();

        if (coins != null)
        {
            builder.AddCoins(coins.Select(_ => new NBitcoin.Coin(new uint256(_.Outpoint.Hash.ToBytes()), _.Outpoint.N,
                new Money(_.Amount.Satoshi), new Script(_.ScriptPubKey.ToBytes()))));    
        }
        
        var nBitcoinTrx = NBitcoin.Transaction.Parse(trx.ToHex(), nbitcoinNetwork);
        
        Assert.True(builder.Verify(nBitcoinTrx, out NBitcoin.Policy.TransactionPolicyError[] errors),
            userMessage: errors.Select(_ => _.ToString()).Aggregate("", (x, y) => x + "," + y));
    }
}
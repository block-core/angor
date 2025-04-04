using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Policy;
using Blockcore.Networks;


namespace Angor.Shared.Services
{
    public class AngorBtcTransactionBuilder : IBitcoinTransactionBuilder
    {
        private TransactionBuilder _builder;
        

        public IBitcoinTransactionBuilder CreateTransactionBuilder(Network network)
        {
            _builder = new TransactionBuilder(network);
            return this;
        }

        public IBitcoinTransactionBuilder Send<TDestination>(TDestination destination, Money amount)
        {
            switch (destination)
            {
                case Script script:
                    _builder.Send(script, amount);
                    break;
                case BitcoinAddress address:
                    _builder.Send(address, amount);
                    break;
                default:
                    _builder.Send(destination as IDestination ?? throw new ArgumentException("Invalid destination type", nameof(destination)),
                        Money.Coins(Convert.ToDecimal(amount)));
                    break;
            }

            return this;
        }

        public IBitcoinTransactionBuilder AddCoins<T>(IEnumerable<T> coins)
        {
            if (coins == null) return this;
            _builder.AddCoins((IEnumerable<ICoin>)coins);
            return this;
        }

        public IBitcoinTransactionBuilder SetChange<T>(T change)
        {
            switch (change)
            {
                case Script script:
                    _builder.SetChange(script);
                    break;
                case BitcoinAddress address:
                    _builder.SetChange(address);
                    break;
                default:
                    _builder.SetChange(change as IDestination ?? throw new ArgumentException("Invalid change destination type", nameof(change)));
                    break;
            }

            return this;
        }

        public IBitcoinTransactionBuilder SendEstimatedFees<T>(T feeRate)
        {
            if (feeRate is FeeRate rate)
            {
                var estimatedFee = _builder.EstimateFees(rate);

                var minimumFee = GetMinimumFee();

                if (estimatedFee < minimumFee)
                {
                    _builder.SendFees(minimumFee);
                }
                else
                {
                    _builder.SendEstimatedFees(rate);
                }
            }
            else
            {
                throw new ArgumentException("Invalid fee rate type", nameof(feeRate));
            }

            return this;
        }

        public Money EstimateFees<T>(T feeRate)
        {
            var builderFee = _builder.EstimateFees(feeRate as FeeRate ?? throw new ArgumentException("Invalid fee rate type", nameof(feeRate)));
            var minimumFee = GetMinimumFee();

            return minimumFee > builderFee ? minimumFee : builderFee;

        }

        public Money EstimateFees<T>(Transaction trx, T feeRate)
        {
            return _builder.EstimateFees(trx ,feeRate as FeeRate ?? throw new ArgumentException("Invalid fee rate type", nameof(feeRate)));
        }

        public IBitcoinTransactionBuilder AddCoin<T>(T coin)
        {
            if (coin is Coin nbitcoinCoin)
            {
                _builder.AddCoins(nbitcoinCoin);
            }
            else
            {
                _builder.AddCoins(coin as Coin ?? throw new ArgumentException("Invalid coin type", nameof(coin)));
            }

            return this;
        }

        public Transaction BuildTransaction(bool sign = true)
        {
            return _builder.BuildTransaction(sign);
        }

        public bool Verify<T, TError>(T trx, out TError[] errors)
        {
            if (trx is Transaction transaction)
            {
                var result = _builder.Verify(transaction, out TransactionPolicyError[] policyErrors);
                errors = policyErrors.Cast<TError>().ToArray();
                return result;
            }

            errors = [];
            return false;
        }

        public Money GetMinimumFee()
        {
            var txSize = _builder.BuildTransaction(true)?
                .GetVirtualSize(4) ?? 0;
            
            return new FeeRate(1000).GetFee(txSize);
        }

        public IBitcoinTransactionBuilder AddKeys<T>(T[] keys)
        {
            if (keys == null) return this;

            foreach (var key in keys)
            {
                if (key is Key keyObj)
                {
                    _builder.AddKeys(keyObj);
                }
            }

            return this;
        }

        public IBitcoinTransactionBuilder ContinueToBuild<T>(T transaction)
        {
            if (transaction is not Transaction tx) 
                return this;

            _builder.ContinueToBuild(tx);

            return this;
        }

        public IBitcoinTransactionBuilder CoverTheRest()
        {
            _builder.CoverTheRest();
            return this;
        }

        public IBitcoinTransactionBuilder SendFees(Money minimumFee)
        {
            _builder.SendFees(minimumFee);
            return this;
        }
    }
}

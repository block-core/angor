using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Protocol;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;
using Blockcore.NBitcoin.DataEncoders;
using System.Collections.Generic;

namespace Angor.Test
{
    public class InvestmentOperationsTest
    {
        [Fact]
        public void BuildStage()
        {

            var funderKey = new Key();
            var investorKey = new Key();
            var secret = new Key();



            var scripts = ScriptBuilder.BuildSeederScript(funderKey.PubKey.ToHex(), investorKey.PubKey.ToHex(), Hashes.Hash256(secret.ToBytes()).ToString(), 5, 10);

            var adress = AngorScripts.CreateStageSeeder(Networks.Bitcoin.Testnet(), scripts.founder, scripts.recover, scripts.endOfProject);

        }



        [Fact]
        public void SpendFounderStageTest()
        {
            var network = Networks.Bitcoin.Testnet();

            var angorKey = new Key();
            var funderKey = new Key();
            var funderReceiveCoinsKey = new Key();
            var investorChangeKey = new Key();
            var investorKey = new Key();
            var secret = new Key();

            InvestmentOperations operations = new InvestmentOperations(new WalletOperationsMock());

            InvestorContext context = new InvestorContext();
            context.ProjectInvestmentInfo = new ProjectInvestmentInfo();
            context.ProjectInvestmentInfo.Stages = new List<Stage>
            {
                new Stage { AmountToRelease = 1, NumberOfBLocks = 10 },
                new Stage { AmountToRelease = 1, NumberOfBLocks = 20 },
                new Stage { AmountToRelease = 1, NumberOfBLocks = 30 }
            };
            context.ProjectInvestmentInfo.FounderKey = Encoders.Hex.EncodeData(funderKey.PubKey.ToBytes());
            context.ProjectInvestmentInfo.AngorFeeKey = Encoders.Hex.EncodeData(angorKey.PubKey.ToBytes());
            context.InvestorKey = Encoders.Hex.EncodeData(investorKey.PubKey.ToBytes());
            context.ChangeAddress = secret.PubKey.GetSegwitAddress(network).ToString();
            context.InvestorSecretHash = Encoders.Hex.EncodeData(Hashes.Hash256(secret.ToBytes()).ToBytes());
            context.ProjectInvestmentInfo.TargetAmount = 3;

            var invtrx = operations.CreateSeederTransaction(network, context, Money.Coins(3).Satoshi);

            operations.SignInvestmentTransaction(network, context, invtrx, null, new List<UtxoDataWithPath>());

            var foundertrx = operations.SpendFounderStage(network, context, 1, funderReceiveCoinsKey.PubKey.ScriptPubKey, Encoders.Hex.EncodeData(funderKey.ToBytes()));

        }

        public class WalletOperationsMock : IWalletOperations
        {
            public string GenerateWalletWords()
            {
                throw new NotImplementedException();
            }

            public Task<OperationResult<Transaction>> SendAmountToAddress(WalletWords walletWords, SendInfo sendInfo)
            {
                throw new NotImplementedException();
            }

            public AccountInfo BuildAccountInfoForWalletWords(WalletWords walletWords)
            {
                throw new NotImplementedException();
            }

            public Task<AccountInfo> FetchDataForExistingAddressesAsync(AccountInfo accountInfo)
            {
                throw new NotImplementedException();
            }

            public Task<AccountInfo> FetchDataForNewAddressesAsync(AccountInfo accountInfo)
            {
                throw new NotImplementedException();
            }

            public Task<(string address, List<UtxoData> data)> FetchUtxoForAddressAsync(string adddress)
            {
                throw new NotImplementedException();
            }

            public Task<IEnumerable<FeeEstimation>> GetFeeEstimationAsync()
            {
                var list = new List<FeeEstimation>
                {
                    new FeeEstimation { Confirmations = 1, FeeRate = 10000 },
                };

                return Task.FromResult<IEnumerable<FeeEstimation>>(list);
            }

            public decimal CalculateTransactionFee(SendInfo sendInfo, AccountInfo accountInfo, long feeRate)
            {
                throw new NotImplementedException();
            }

            public (List<Coin>? coins, List<Key> keys) GetUnspentOutputsForTransaction(WalletWords walletWords, List<UtxoDataWithPath> utxoDataWithPaths)
            {
                var network = Shared.Networks.Networks.Bitcoin.Testnet();

                // create a fake inputTrx
                var fakeInputTrx = network.Consensus.ConsensusFactory.CreateTransaction();
                Key fakeInputKey = new Key();
                var fakeTxout = fakeInputTrx.AddOutput(Money.Parse("20.2"), fakeInputKey.ScriptPubKey);

                List<Coin> coins = new List<Coin>();

                List<Key> keys = new List<Key>()
                {
                    { fakeInputKey },
                };

                foreach (var key in keys)
                {
                    coins.Add(new Coin(fakeInputTrx, fakeTxout));
                }

                return (coins, keys);

            }
        }
    }
}
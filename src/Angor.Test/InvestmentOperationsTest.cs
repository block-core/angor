using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Protocol;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;
using Blockcore.NBitcoin.DataEncoders;
using Moq;

namespace Angor.Test
{
    public class InvestmentOperationsTest
    {
        private Mock<IWalletOperations> _walletOperations;


        public InvestmentOperationsTest()
        {
            _walletOperations = new Mock<IWalletOperations>();

            _walletOperations.Setup(_ => _.GetFeeEstimationAsync())
                .ReturnsAsync(new List<FeeEstimation>
                {
                    new() { Confirmations = 1, FeeRate = 10000 },
                });

            _walletOperations.Setup(_ => _.GetUnspentOutputsForTransaction(It.IsAny<WalletWords>(),
                        It.IsAny<List<UtxoDataWithPath>>()))
                .Returns<WalletWords, List<UtxoDataWithPath>>((_, _) =>
                {
                    var network = Networks.Bitcoin.Testnet();

                    // create a fake inputTrx
                    var fakeInputTrx = network.Consensus.ConsensusFactory.CreateTransaction();
                    var fakeInputKey = new Key();
                    var fakeTxout = fakeInputTrx.AddOutput(Money.Parse("20.2"), fakeInputKey.ScriptPubKey);

                    var keys = new List<Key> { fakeInputKey };

                    var coins = keys.Select(key => new Coin(fakeInputTrx, fakeTxout)).ToList();

                    return (coins, keys);
                });

        }

        [Fact]
        public void BuildStage()
        {

            var funderKey = new Key();
            var investorKey = new Key();
            var secret = new Key();



            var scripts = ScriptBuilder.BuildSeederScript(funderKey.PubKey.ToHex(), investorKey.PubKey.ToHex(), Hashes.Hash256(secret.ToBytes()).ToString(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1));

            var adress = AngorScripts.CreateStageSeeder(Networks.Bitcoin.Testnet(), scripts.founder, scripts.recover, scripts.endOfProject);

        }



        [Fact]
        public void SpendFounderStageTest()
        {
            var network = Networks.Bitcoin.Testnet();

            var angorKey = new Key();
            var funderKey = new Key();
            var funderReceiveCoinsKey = new Key();
            var investorKey = new Key();
            var investorChangeKey = new Key();
            var investorReceiveCoinsKey = new Key();
            var secret = new Key();

            InvestmentOperations operations = new InvestmentOperations(_walletOperations.Object);

            InvestorContext context = new InvestorContext();
            context.ProjectInvestmentInfo = new ProjectInvestmentInfo();
            context.ProjectInvestmentInfo.TargetAmount = 3;
            context.ProjectInvestmentInfo.StartDate = DateTime.UtcNow;
            context.ProjectInvestmentInfo.ExpiryDate = DateTime.UtcNow.AddDays(5);
            context.ProjectInvestmentInfo.Stages = new List<Stage>
            {
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(2) },
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(3) }
            };
            context.ProjectInvestmentInfo.FounderKey = Encoders.Hex.EncodeData(funderKey.PubKey.ToBytes());
            context.ProjectInvestmentInfo.AngorFeeKey = Encoders.Hex.EncodeData(angorKey.PubKey.ToBytes());
            context.InvestorKey = Encoders.Hex.EncodeData(investorKey.PubKey.ToBytes());
            context.ChangeAddress = investorChangeKey.PubKey.GetSegwitAddress(network).ToString();
            context.InvestorSecretHash = Encoders.Hex.EncodeData(Hashes.Hash256(secret.ToBytes()).ToBytes());

            var invtrx = operations.CreateSeederTransaction(network, context, Money.Coins(3).Satoshi);

            operations.SignInvestmentTransaction(network, context, invtrx, null, new List<UtxoDataWithPath>());

            var foundertrx = operations.SpendFounderStage(network, context, 1, funderReceiveCoinsKey.PubKey.ScriptPubKey, Encoders.Hex.EncodeData(funderKey.ToBytes()));
            
            Assert.NotNull(foundertrx);

            var investorExpierytrx = operations.RecoverEndOfProjectFunds(network, context, 1, investorReceiveCoinsKey.PubKey.ScriptPubKey, Encoders.Hex.EncodeData(investorKey.ToBytes()));

        }
    }
}
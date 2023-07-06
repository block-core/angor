using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Protocol;
using Blockcore.Consensus.TransactionInfo;
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

            InvestmentOperations operations = new InvestmentOperations(_walletOperations.Object);

            var projectInvestmentInfo = new ProjectInvestmentInfo();
            projectInvestmentInfo.TargetAmount = 3;
            projectInvestmentInfo.StartDate = DateTime.UtcNow;
            projectInvestmentInfo.ExpiryDate = DateTime.UtcNow.AddDays(5);
            projectInvestmentInfo.Stages = new List<Stage>
            {
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(2) },
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(3) }
            };
            projectInvestmentInfo.FounderKey = Encoders.Hex.EncodeData(funderKey.PubKey.ToBytes());
            projectInvestmentInfo.AngorFeeKey = Encoders.Hex.EncodeData(angorKey.PubKey.ToBytes());

            // Create the seeder 1 params
            var seeder11Key = new Key();
            var seeder1secret = new Key();
            var seeder1ChangeKey = new Key();
            var seeder1ReceiveCoinsKey = new Key();

            InvestorContext seeder1Context = new InvestorContext() { ProjectInvestmentInfo = projectInvestmentInfo };

            seeder1Context.InvestorKey = Encoders.Hex.EncodeData(seeder11Key.PubKey.ToBytes());
            seeder1Context.ChangeAddress = seeder1ChangeKey.PubKey.GetSegwitAddress(network).ToString();
            seeder1Context.InvestorSecretHash = Encoders.Hex.EncodeData(Hashes.Hash256(seeder1secret.ToBytes()).ToBytes());

            // Create the seeder 2 params
            var seeder2Key = new Key();
            var seeder2secret = new Key();
            var seeder2ChangeKey = new Key();
            var seeder2ReceiveCoinsKey = new Key();

            InvestorContext seeder2Context = new InvestorContext() { ProjectInvestmentInfo = projectInvestmentInfo };

            seeder2Context.InvestorKey = Encoders.Hex.EncodeData(seeder2Key.PubKey.ToBytes());
            seeder2Context.ChangeAddress = seeder2ChangeKey.PubKey.GetSegwitAddress(network).ToString();
            seeder2Context.InvestorSecretHash = Encoders.Hex.EncodeData(Hashes.Hash256(seeder2secret.ToBytes()).ToBytes());

            FounderContext founderContext = new FounderContext { ProjectInvestmentInfo = projectInvestmentInfo };


            // create the founders transaction with both seeders

            var seeder1InvTrx = operations.CreateSeederInvestmentTransaction(network, seeder1Context, Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

            operations.SignInvestmentTransaction(network, seeder1Context, seeder1InvTrx, null, new List<UtxoDataWithPath>());

            founderContext.InvestmentTrasnactionsHex.Add(seeder1Context.TransactionHex);

            var seeder2InvTrx = operations.CreateSeederInvestmentTransaction(network, seeder2Context, Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

            operations.SignInvestmentTransaction(network, seeder2Context, seeder2InvTrx, null, new List<UtxoDataWithPath>());

            founderContext.InvestmentTrasnactionsHex.Add(seeder2Context.TransactionHex);

            var founderTrxForSeeder1Stage1 = operations.SpendFounderStage(network, founderContext, 1, funderReceiveCoinsKey.PubKey.ScriptPubKey, Encoders.Hex.EncodeData(funderKey.ToBytes()));
            
        }

        [Fact]
        public void SpendInvestorEndOfProjectTest()
        {
            var network = Networks.Bitcoin.Testnet();

            var angorKey = new Key();
            var funderKey = new Key();
            var funderReceiveCoinsKey = new Key();

            InvestmentOperations operations = new InvestmentOperations(_walletOperations.Object);

            var projectInvestmentInfo = new ProjectInvestmentInfo();
            projectInvestmentInfo.TargetAmount = 3;
            projectInvestmentInfo.StartDate = DateTime.UtcNow;
            projectInvestmentInfo.ExpiryDate = DateTime.UtcNow.AddDays(5);
            projectInvestmentInfo.Stages = new List<Stage>
            {
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(2) },
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(3) }
            };
            projectInvestmentInfo.FounderKey = Encoders.Hex.EncodeData(funderKey.PubKey.ToBytes());
            projectInvestmentInfo.AngorFeeKey = Encoders.Hex.EncodeData(angorKey.PubKey.ToBytes());

            // Create the seeder 1 params
            var seeder11Key = new Key();
            var seeder1secret = new Key();
            var seeder1ChangeKey = new Key();
            var seeder1ReceiveCoinsKey = new Key();

            InvestorContext seeder1Context = new InvestorContext() { ProjectInvestmentInfo = projectInvestmentInfo };

            seeder1Context.InvestorKey = Encoders.Hex.EncodeData(seeder11Key.PubKey.ToBytes());
            seeder1Context.ChangeAddress = seeder1ChangeKey.PubKey.GetSegwitAddress(network).ToString();
            seeder1Context.InvestorSecretHash = Encoders.Hex.EncodeData(Hashes.Hash256(seeder1secret.ToBytes()).ToBytes());

            // create the investment transaction

            var seeder1InvTrx = operations.CreateSeederInvestmentTransaction(network, seeder1Context, Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

            operations.SignInvestmentTransaction(network, seeder1Context, seeder1InvTrx, null, new List<UtxoDataWithPath>());

            var seeder1Expierytrx = operations.RecoverEndOfProjectFunds(network, seeder1Context, 1, seeder1ReceiveCoinsKey.PubKey.ScriptPubKey, Encoders.Hex.EncodeData(seeder11Key.ToBytes()));

            Assert.NotNull(seeder1Expierytrx);
        }
    }
}
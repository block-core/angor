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
        public void SpendFounderStage_Test()
        {
            var network = Networks.Bitcoin.Testnet();

            var angorKey = new Key();
            var funderKey = new Key();
            var funderReceiveCoinsKey = new Key();

            InvestmentOperations operations = new InvestmentOperations(_walletOperations.Object);

            var projectInvestmentInfo = new ProjectInfo();
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
            var seeder1Key = new Key();
            var seeder1secret = new Key();
            var seeder1ChangeKey = new Key();

            InvestorContext seeder1Context = new InvestorContext() { ProjectInfo = projectInvestmentInfo };

            seeder1Context.InvestorKey = Encoders.Hex.EncodeData(seeder1Key.PubKey.ToBytes());
            seeder1Context.ChangeAddress = seeder1ChangeKey.PubKey.GetSegwitAddress(network).ToString();
            seeder1Context.InvestorSecretHash = Encoders.Hex.EncodeData(Hashes.Hash256(seeder1secret.ToBytes()).ToBytes());

            // Create the seeder 2 params
            var seeder2Key = new Key();
            var seeder2secret = new Key();
            var seeder2ChangeKey = new Key();

            InvestorContext seeder2Context = new InvestorContext() { ProjectInfo = projectInvestmentInfo };

            seeder2Context.InvestorKey = Encoders.Hex.EncodeData(seeder2Key.PubKey.ToBytes());
            seeder2Context.ChangeAddress = seeder2ChangeKey.PubKey.GetSegwitAddress(network).ToString();
            seeder2Context.InvestorSecretHash = Encoders.Hex.EncodeData(Hashes.Hash256(seeder2secret.ToBytes()).ToBytes());

            // Create the seeder 3 params
            var seeder3Key = new Key();
            var seeder3secret = new Key();
            var seeder3ChangeKey = new Key();

            InvestorContext seeder3Context = new InvestorContext() { ProjectInfo = projectInvestmentInfo };

            seeder3Context.InvestorKey = Encoders.Hex.EncodeData(seeder3Key.PubKey.ToBytes());
            seeder3Context.ChangeAddress = seeder3ChangeKey.PubKey.GetSegwitAddress(network).ToString();
            seeder3Context.InvestorSecretHash = Encoders.Hex.EncodeData(Hashes.Hash256(seeder3secret.ToBytes()).ToBytes());

            // Build seeders hashes

            ProjectSeeders projectSeeders = new ProjectSeeders();
            projectSeeders.Threshold = 2;
            projectSeeders.SecretHashes.Add(seeder1Context.InvestorSecretHash);
            projectSeeders.SecretHashes.Add(seeder2Context.InvestorSecretHash);
            projectSeeders.SecretHashes.Add(seeder3Context.InvestorSecretHash);

            // Create the investor 1 params
            var investor1Key = new Key();
            var investor1ChangeKey = new Key();

            InvestorContext investor1Context = new InvestorContext() { ProjectInfo = projectInvestmentInfo, ProjectSeeders = projectSeeders };

            investor1Context.InvestorKey = Encoders.Hex.EncodeData(investor1Key.PubKey.ToBytes());
            investor1Context.ChangeAddress = investor1ChangeKey.PubKey.GetSegwitAddress(network).ToString();

            // Create the investor 2 params
            var investor2Key = new Key();
            var investor2ChangeKey = new Key();

            InvestorContext investor2Context = new InvestorContext() { ProjectInfo = projectInvestmentInfo, ProjectSeeders = projectSeeders };

            investor2Context.InvestorKey = Encoders.Hex.EncodeData(investor2Key.PubKey.ToBytes());
            investor2Context.ChangeAddress = investor2ChangeKey.PubKey.GetSegwitAddress(network).ToString();

            // create founder context
            FounderContext founderContext = new FounderContext { ProjectInfo = projectInvestmentInfo, ProjectSeeders = projectSeeders };

            // create seeder1 investment transaction

            var seeder1InvTrx = operations.CreateInvestmentTransaction(network, seeder1Context, Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

            operations.SignInvestmentTransaction(network, seeder1Context, seeder1InvTrx, null, new List<UtxoDataWithPath>());

            founderContext.InvestmentTrasnactionsHex.Add(seeder1Context.TransactionHex);

            // create seeder2 investment transaction

            var seeder2InvTrx = operations.CreateInvestmentTransaction(network, seeder2Context, Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

            operations.SignInvestmentTransaction(network, seeder2Context, seeder2InvTrx, null, new List<UtxoDataWithPath>());

            founderContext.InvestmentTrasnactionsHex.Add(seeder2Context.TransactionHex);

            // create seeder3 investment transaction

            var seeder3InvTrx = operations.CreateInvestmentTransaction(network, seeder3Context, Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

            operations.SignInvestmentTransaction(network, seeder3Context, seeder3InvTrx, null, new List<UtxoDataWithPath>());

            founderContext.InvestmentTrasnactionsHex.Add(seeder3Context.TransactionHex);

            // create investor 1 investment transaction

            var investor1InvTrx = operations.CreateInvestmentTransaction(network, investor1Context, Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

            operations.SignInvestmentTransaction(network, investor1Context, investor1InvTrx, null, new List<UtxoDataWithPath>());

            founderContext.InvestmentTrasnactionsHex.Add(investor1Context.TransactionHex);

            // create investor 2 investment transaction

            var investor2InvTrx = operations.CreateInvestmentTransaction(network, investor2Context, Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

            operations.SignInvestmentTransaction(network, investor2Context, investor2InvTrx, null, new List<UtxoDataWithPath>());

            founderContext.InvestmentTrasnactionsHex.Add(investor2Context.TransactionHex);

            // spend all investment transactions for stage 1

            var founderTrxForSeeder1Stage1 = operations.SpendFounderStage(network, founderContext, 1, funderReceiveCoinsKey.PubKey.ScriptPubKey, Encoders.Hex.EncodeData(funderKey.ToBytes()));
            
        }

        [Fact]
        public void SeederTransaction_EndOfProject_Test()
        {
            var network = Networks.Bitcoin.Testnet();

            var angorKey = new Key();
            var funderKey = new Key();
            var funderReceiveCoinsKey = new Key();

            InvestmentOperations operations = new InvestmentOperations(_walletOperations.Object);

            var projectInvestmentInfo = new ProjectInfo();
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

            InvestorContext seeder1Context = new InvestorContext() { ProjectInfo = projectInvestmentInfo };

            seeder1Context.InvestorKey = Encoders.Hex.EncodeData(seeder11Key.PubKey.ToBytes());
            seeder1Context.ChangeAddress = seeder1ChangeKey.PubKey.GetSegwitAddress(network).ToString();
            seeder1Context.InvestorSecretHash = Encoders.Hex.EncodeData(Hashes.Hash256(seeder1secret.ToBytes()).ToBytes());

            // create the investment transaction

            var seeder1InvTrx = operations.CreateInvestmentTransaction(network, seeder1Context, Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

            operations.SignInvestmentTransaction(network, seeder1Context, seeder1InvTrx, null, new List<UtxoDataWithPath>());

            var seeder1Expierytrx = operations.RecoverEndOfProjectFunds(network, seeder1Context, new[] { 2, 3 }, seeder1ReceiveCoinsKey.PubKey.ScriptPubKey, Encoders.Hex.EncodeData(seeder11Key.ToBytes()));

            Assert.NotNull(seeder1Expierytrx);
        }

        [Fact]
        public void InvestorTransaction_EndOfProject_Test()
        {
            var network = Networks.Bitcoin.Testnet();

            var angorKey = new Key();
            var funderKey = new Key();
            var funderReceiveCoinsKey = new Key();

            InvestmentOperations operations = new InvestmentOperations(_walletOperations.Object);

            var projectInvestmentInfo = new ProjectInfo();
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
            var seeder1ChangeKey = new Key();
            var seeder1ReceiveCoinsKey = new Key();

            InvestorContext seeder1Context = new InvestorContext() { ProjectInfo = projectInvestmentInfo, ProjectSeeders = new ProjectSeeders()};

            seeder1Context.InvestorKey = Encoders.Hex.EncodeData(seeder11Key.PubKey.ToBytes());
            seeder1Context.ChangeAddress = seeder1ChangeKey.PubKey.GetSegwitAddress(network).ToString();

            // create the investment transaction

            var seeder1InvTrx = operations.CreateInvestmentTransaction(network, seeder1Context, Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

            operations.SignInvestmentTransaction(network, seeder1Context, seeder1InvTrx, null, new List<UtxoDataWithPath>());

            var seeder1Expierytrx = operations.RecoverEndOfProjectFunds(network, seeder1Context, new[] { 2, 3 }, seeder1ReceiveCoinsKey.PubKey.ScriptPubKey, Encoders.Hex.EncodeData(seeder11Key.ToBytes()));

            Assert.NotNull(seeder1Expierytrx);
        }

        [Fact]
        public void InvestorTransaction_WithSeederHashes_EndOfProject_Test()
        {
            var network = Networks.Bitcoin.Testnet();

            var angorKey = new Key();
            var funderKey = new Key();
            var funderReceiveCoinsKey = new Key();

            InvestmentOperations operations = new InvestmentOperations(_walletOperations.Object);

            var projectInvestmentInfo = new ProjectInfo();
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
            var seeder1ChangeKey = new Key();
            var seeder1ReceiveCoinsKey = new Key();

            ProjectSeeders projectSeeders = new ProjectSeeders();
            projectSeeders.Threshold = 2;
            projectSeeders.SecretHashes.Add(Hashes.Hash256(new Key().ToBytes()).ToString());
            projectSeeders.SecretHashes.Add(Hashes.Hash256(new Key().ToBytes()).ToString());
            projectSeeders.SecretHashes.Add(Hashes.Hash256(new Key().ToBytes()).ToString());

            InvestorContext seeder1Context = new InvestorContext() { ProjectInfo = projectInvestmentInfo, ProjectSeeders = projectSeeders };

            seeder1Context.InvestorKey = Encoders.Hex.EncodeData(seeder11Key.PubKey.ToBytes());
            seeder1Context.ChangeAddress = seeder1ChangeKey.PubKey.GetSegwitAddress(network).ToString();

            // create the investment transaction

            var seeder1InvTrx = operations.CreateInvestmentTransaction(network, seeder1Context, Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

            operations.SignInvestmentTransaction(network, seeder1Context, seeder1InvTrx, null, new List<UtxoDataWithPath>());

            var seeder1Expierytrx = operations.RecoverEndOfProjectFunds(network, seeder1Context, new[] { 2, 3 }, seeder1ReceiveCoinsKey.PubKey.ScriptPubKey, Encoders.Hex.EncodeData(seeder11Key.ToBytes()));

            Assert.NotNull(seeder1Expierytrx);
        }

        [Fact]
        public void InvestorTransaction_NoPenalty_Test()
        {
            var network = Networks.Bitcoin.Testnet();

            var angorKey = new Key();
            var funderKey = new Key();
            var funderReceiveCoinsKey = new Key();

            InvestmentOperations operations = new InvestmentOperations(_walletOperations.Object);

            var projectInvestmentInfo = new ProjectInfo();
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
            var seeder1ChangeKey = new Key();
            var seeder1ReceiveCoinsKey = new Key();

            var seeder1Key = new Key();
            var seeder2Key = new Key();
            var seeder3Key = new Key();

            ProjectSeeders projectSeeders = new ProjectSeeders();
            projectSeeders.Threshold = 2;
            projectSeeders.SecretHashes.Add(Hashes.Hash256(seeder1Key.ToBytes()).ToString());
            projectSeeders.SecretHashes.Add(Hashes.Hash256(seeder2Key.ToBytes()).ToString());
            projectSeeders.SecretHashes.Add(Hashes.Hash256(seeder3Key.ToBytes()).ToString());

            InvestorContext seeder1Context = new InvestorContext() { ProjectInfo = projectInvestmentInfo, ProjectSeeders = projectSeeders };

            seeder1Context.InvestorKey = Encoders.Hex.EncodeData(seeder11Key.PubKey.ToBytes());
            seeder1Context.ChangeAddress = seeder1ChangeKey.PubKey.GetSegwitAddress(network).ToString();

            // create the investment transaction

            var seeder1InvTrx = operations.CreateInvestmentTransaction(network, seeder1Context, Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

            operations.SignInvestmentTransaction(network, seeder1Context, seeder1InvTrx, null, new List<UtxoDataWithPath>());

            var seeder1Expierytrx = operations.RecoverFundsNoPenalty(network, seeder1Context, new[] { 2, 3 }, new Key[]{ seeder2Key, seeder3Key }, seeder1ReceiveCoinsKey.PubKey.ScriptPubKey, Encoders.Hex.EncodeData(seeder11Key.ToBytes()));

            Assert.NotNull(seeder1Expierytrx);
        }
    }
}
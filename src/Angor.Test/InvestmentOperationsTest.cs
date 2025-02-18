using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Test.DataBuilders;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;
using Blockcore.NBitcoin.DataEncoders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NBitcoin;
using NBitcoin.Policy;
using Coin = Blockcore.NBitcoin.Coin;
using Key = Blockcore.NBitcoin.Key;
using Money = Blockcore.NBitcoin.Money;
using Transaction = NBitcoin.Transaction;

namespace Angor.Test
{
    public class InvestmentOperationsTest
    {
        private Mock<IWalletOperations> _walletOperations;
        private Mock<INetworkConfiguration> _networkConfiguration;

        private string angorRootKey =
            "tpubD8JfN1evVWPoJmLgVg6Usq2HEW9tLqm6CyECAADnH5tyQosrL6NuhpL9X1cQCbSmndVrgLSGGdbRqLfUbE6cRqUbrHtDJgSyQEY2Uu7WwTL";

        private FeeEstimation _expectedFeeEstimation = new FeeEstimation()
            { Confirmations = 1, FeeRate = 10000 };

        public InvestmentOperationsTest()
        {
            _walletOperations = new Mock<IWalletOperations>();

            _walletOperations.Setup(_ => _.GetFeeEstimationAsync())
                .ReturnsAsync(new List<FeeEstimation> { _expectedFeeEstimation });

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

            _networkConfiguration = new Mock<INetworkConfiguration>();

            _networkConfiguration.Setup(_ => _.GetNetwork())
                .Returns(Networks.Bitcoin.Testnet());
        }

        [Fact]
        public void SpendFounderStage_Test()
        {
            DerivationOperations derivationOperations = new DerivationOperations(new HdOperations(),
                new NullLogger<DerivationOperations>(), _networkConfiguration.Object);
            InvestmentOperations operations = new InvestmentOperations(_walletOperations.Object, derivationOperations);

            var network = Networks.Bitcoin.Testnet();

            var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

            var funderKey = derivationOperations.DeriveFounderPrivateKey(words, 1);

            var funderReceiveCoinsKey = new Key();

            var projectInvestmentInfo = new ProjectInfo();
            projectInvestmentInfo.TargetAmount = Money.Coins(3).Satoshi;
            projectInvestmentInfo.StartDate = DateTime.UtcNow;
            projectInvestmentInfo.ExpiryDate = DateTime.UtcNow.AddDays(5);
            projectInvestmentInfo.Stages = new List<Stage>
            {
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(2) },
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(3) }
            };
            projectInvestmentInfo.FounderKey = derivationOperations.DeriveFounderKey(words, 1);
            projectInvestmentInfo.FounderRecoveryKey = derivationOperations.DeriveFounderRecoveryKey(words, 1);
            projectInvestmentInfo.ProjectIdentifier =
                derivationOperations.DeriveAngorKey(projectInvestmentInfo.FounderKey, angorRootKey);

            // Create the seeder 1 params
            var seeder1Key = new Key();
            var seeder1secret = new Key();
            var seeder1ChangeKey = new Key();

            InvestorContext seeder1Context = new InvestorContext() { ProjectInfo = projectInvestmentInfo };

            seeder1Context.InvestorKey = Encoders.Hex.EncodeData(seeder1Key.PubKey.ToBytes());
            seeder1Context.ChangeAddress = seeder1ChangeKey.PubKey.GetSegwitAddress(network).ToString();
            seeder1Context.InvestorSecretHash = Hashes.Hash256(seeder1secret.ToBytes()).ToString();

            // Create the seeder 2 params
            var seeder2Key = new Key();
            var seeder2secret = new Key();
            var seeder2ChangeKey = new Key();

            InvestorContext seeder2Context = new InvestorContext() { ProjectInfo = projectInvestmentInfo };

            seeder2Context.InvestorKey = Encoders.Hex.EncodeData(seeder2Key.PubKey.ToBytes());
            seeder2Context.ChangeAddress = seeder2ChangeKey.PubKey.GetSegwitAddress(network).ToString();
            seeder2Context.InvestorSecretHash = Hashes.Hash256(seeder2secret.ToBytes()).ToString();

            // Create the seeder 3 params
            var seeder3Key = new Key();
            var seeder3secret = new Key();
            var seeder3ChangeKey = new Key();

            InvestorContext seeder3Context = new InvestorContext() { ProjectInfo = projectInvestmentInfo };

            seeder3Context.InvestorKey = Encoders.Hex.EncodeData(seeder3Key.PubKey.ToBytes());
            seeder3Context.ChangeAddress = seeder3ChangeKey.PubKey.GetSegwitAddress(network).ToString();
            seeder3Context.InvestorSecretHash = Hashes.Hash256(seeder3secret.ToBytes()).ToString();

            // Build seeders hashes

            ProjectSeeders projectSeeders = new ProjectSeeders();
            projectSeeders.Threshold = 2;
            projectSeeders.SecretHashes.Add(seeder1Context.InvestorSecretHash);
            projectSeeders.SecretHashes.Add(seeder2Context.InvestorSecretHash);
            projectSeeders.SecretHashes.Add(seeder3Context.InvestorSecretHash);

            projectInvestmentInfo.ProjectSeeders = projectSeeders;

            // Create the investor 1 params
            var investor1Key = new Key();
            var investor1ChangeKey = new Key();

            InvestorContext investor1Context = new InvestorContext() { ProjectInfo = projectInvestmentInfo };

            investor1Context.InvestorKey = Encoders.Hex.EncodeData(investor1Key.PubKey.ToBytes());
            investor1Context.ChangeAddress = investor1ChangeKey.PubKey.GetSegwitAddress(network).ToString();

            // Create the investor 2 params
            var investor2Key = new Key();
            var investor2ChangeKey = new Key();

            InvestorContext investor2Context = new InvestorContext() { ProjectInfo = projectInvestmentInfo };

            investor2Context.InvestorKey = Encoders.Hex.EncodeData(investor2Key.PubKey.ToBytes());
            investor2Context.ChangeAddress = investor2ChangeKey.PubKey.GetSegwitAddress(network).ToString();

            // create founder context
            FounderContext founderContext = new FounderContext
                { ProjectInfo = projectInvestmentInfo, ProjectSeeders = projectSeeders };

            // create seeder1 investment transaction

            var seeder1InvTrx = operations.CreateInvestmentTransaction(network, seeder1Context,
                projectInvestmentInfo.TargetAmount);

            seeder1Context.TransactionHex = operations.SignInvestmentTransaction(network, seeder1Context.ChangeAddress,
                    seeder1InvTrx, null, new List<UtxoDataWithPath>(), _expectedFeeEstimation)
                .ToHex();

            founderContext.InvestmentTrasnactionsHex.Add(seeder1Context.TransactionHex);

            // create seeder2 investment transaction

            var seeder2InvTrx = operations.CreateInvestmentTransaction(network, seeder2Context,
                projectInvestmentInfo.TargetAmount);

            seeder2Context.TransactionHex = operations.SignInvestmentTransaction(network, seeder2Context.ChangeAddress,
                    seeder2InvTrx, null, new List<UtxoDataWithPath>(), _expectedFeeEstimation)
                .ToHex();

            founderContext.InvestmentTrasnactionsHex.Add(seeder2Context.TransactionHex);

            // create seeder3 investment transaction

            var seeder3InvTrx = operations.CreateInvestmentTransaction(network, seeder3Context,
                projectInvestmentInfo.TargetAmount);

            seeder3Context.TransactionHex = operations.SignInvestmentTransaction(network, seeder3Context.ChangeAddress,
                    seeder3InvTrx, null, new List<UtxoDataWithPath>(), _expectedFeeEstimation)
                .ToHex();

            founderContext.InvestmentTrasnactionsHex.Add(seeder3Context.TransactionHex);

            // create investor 1 investment transaction

            var investor1InvTrx = operations.CreateInvestmentTransaction(network, investor1Context,
                projectInvestmentInfo.TargetAmount);

            investor1Context.TransactionHex = operations.SignInvestmentTransaction(network,
                    investor1Context.ChangeAddress, investor1InvTrx, null, new List<UtxoDataWithPath>(),
                    _expectedFeeEstimation)
                .ToHex();

            founderContext.InvestmentTrasnactionsHex.Add(investor1Context.TransactionHex);

            // create investor 2 investment transaction

            var investor2InvTrx = operations.CreateInvestmentTransaction(network, investor2Context,
                projectInvestmentInfo.TargetAmount);

            investor2Context.TransactionHex = operations.SignInvestmentTransaction(network,
                    investor2Context.ChangeAddress, investor2InvTrx, null, new List<UtxoDataWithPath>(),
                    _expectedFeeEstimation)
                .ToHex();

            founderContext.InvestmentTrasnactionsHex.Add(investor2Context.TransactionHex);

            // spend all investment transactions for stage 1

            var founderTrxForSeeder1Stage1 = operations.SpendFounderStage(network, founderContext, 1,
                funderReceiveCoinsKey.PubKey.ScriptPubKey, Encoders.Hex.EncodeData(funderKey.ToBytes())
                , _expectedFeeEstimation);

        }

        [Fact]
        public void SeederTransaction_EndOfProject_Test()
        {
            DerivationOperations derivationOperations = new DerivationOperations(new HdOperations(),
                new NullLogger<DerivationOperations>(), _networkConfiguration.Object);
            InvestmentOperations operations = new InvestmentOperations(_walletOperations.Object, derivationOperations);

            var network = Networks.Bitcoin.Testnet();

            var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

            var projectInvestmentInfo = new ProjectInfo();
            projectInvestmentInfo.TargetAmount = Money.Coins(3).Satoshi;
            projectInvestmentInfo.StartDate = DateTime.UtcNow;
            projectInvestmentInfo.ExpiryDate = DateTime.UtcNow.AddDays(5);
            projectInvestmentInfo.Stages = new List<Stage>
            {
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(2) },
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(3) }
            };
            projectInvestmentInfo.FounderKey = derivationOperations.DeriveFounderKey(words, 1);
            projectInvestmentInfo.FounderRecoveryKey = derivationOperations.DeriveFounderRecoveryKey(words, 1);
            projectInvestmentInfo.ProjectIdentifier =
                derivationOperations.DeriveAngorKey(projectInvestmentInfo.FounderKey, angorRootKey);

            // Create the seeder 1 params
            var seeder11Key = new Key();
            var seeder1secret = new Key();
            var seeder1ChangeKey = new Key();
            var seeder1ReceiveCoinsKey = new Key();

            InvestorContext seeder1Context = new InvestorContext() { ProjectInfo = projectInvestmentInfo };

            seeder1Context.InvestorKey = Encoders.Hex.EncodeData(seeder11Key.PubKey.ToBytes());
            seeder1Context.ChangeAddress = seeder1ChangeKey.PubKey.GetSegwitAddress(network).ToString();
            seeder1Context.InvestorSecretHash = Hashes.Hash256(seeder1secret.ToBytes()).ToString();

            // create the investment transaction

            var seeder1InvTrx = operations.CreateInvestmentTransaction(network, seeder1Context,
                projectInvestmentInfo.TargetAmount);

            seeder1Context.TransactionHex = operations.SignInvestmentTransaction(network, seeder1Context.ChangeAddress,
                    seeder1InvTrx, null, new List<UtxoDataWithPath>(), _expectedFeeEstimation)
                .ToHex();

            var seeder1Expierytrx = operations.RecoverEndOfProjectFunds(network, seeder1Context, new[] { 2, 3 },
                seeder1ReceiveCoinsKey.PubKey.ScriptPubKey, Encoders.Hex.EncodeData(seeder11Key.ToBytes()));

            Assert.NotNull(seeder1Expierytrx);
        }

        [Fact]
        public void InvestorTransaction_EndOfProject_Test()
        {
            DerivationOperations derivationOperations = new DerivationOperations(new HdOperations(),
                new NullLogger<DerivationOperations>(), _networkConfiguration.Object);
            InvestmentOperations operations = new InvestmentOperations(_walletOperations.Object, derivationOperations);

            var network = Networks.Bitcoin.Testnet();

            var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

            var projectInvestmentInfo = new ProjectInfo();
            projectInvestmentInfo.TargetAmount = Money.Coins(3).Satoshi;
            projectInvestmentInfo.StartDate = DateTime.UtcNow;
            projectInvestmentInfo.ExpiryDate = DateTime.UtcNow.AddDays(5);
            projectInvestmentInfo.Stages = new List<Stage>
            {
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(2) },
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(3) }
            };
            projectInvestmentInfo.FounderKey = derivationOperations.DeriveFounderKey(words, 1);
            projectInvestmentInfo.FounderRecoveryKey = derivationOperations.DeriveFounderRecoveryKey(words, 1);
            projectInvestmentInfo.ProjectIdentifier =
                derivationOperations.DeriveAngorKey(projectInvestmentInfo.FounderKey, angorRootKey);
            projectInvestmentInfo.ProjectSeeders = new ProjectSeeders();

            // Create the seeder 1 params
            var seeder11Key = new Key();
            var seeder1ChangeKey = new Key();
            var seeder1ReceiveCoinsKey = new Key();

            InvestorContext seeder1Context = new InvestorContext() { ProjectInfo = projectInvestmentInfo };

            seeder1Context.InvestorKey = Encoders.Hex.EncodeData(seeder11Key.PubKey.ToBytes());
            seeder1Context.ChangeAddress = seeder1ChangeKey.PubKey.GetSegwitAddress(network).ToString();

            // create the investment transaction

            var seeder1InvTrx = operations.CreateInvestmentTransaction(network, seeder1Context,
                projectInvestmentInfo.TargetAmount);

            seeder1Context.TransactionHex = operations.SignInvestmentTransaction(network, seeder1Context.ChangeAddress,
                    seeder1InvTrx, null, new List<UtxoDataWithPath>(),
                    _expectedFeeEstimation)
                .ToHex();

            var seeder1Expierytrx = operations.RecoverEndOfProjectFunds(network, seeder1Context, new[] { 2, 3 },
                seeder1ReceiveCoinsKey.PubKey.ScriptPubKey, Encoders.Hex.EncodeData(seeder11Key.ToBytes()));

            Assert.NotNull(seeder1Expierytrx);
        }

        [Fact]
        public void InvestorTransaction_WithSeederHashes_EndOfProject_Test()
        {
            DerivationOperations derivationOperations = new DerivationOperations(new HdOperations(),
                new NullLogger<DerivationOperations>(), _networkConfiguration.Object);
            InvestmentOperations operations = new InvestmentOperations(_walletOperations.Object, derivationOperations);

            var network = Networks.Bitcoin.Testnet();

            var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

            var projectInvestmentInfo = new ProjectInfo();
            projectInvestmentInfo.TargetAmount = Money.Coins(3).Satoshi;
            projectInvestmentInfo.StartDate = DateTime.UtcNow;
            projectInvestmentInfo.ExpiryDate = DateTime.UtcNow.AddDays(5);
            projectInvestmentInfo.Stages = new List<Stage>
            {
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(2) },
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(3) }
            };
            projectInvestmentInfo.FounderKey = derivationOperations.DeriveFounderKey(words, 1);
            projectInvestmentInfo.FounderRecoveryKey = derivationOperations.DeriveFounderRecoveryKey(words, 1);
            projectInvestmentInfo.ProjectIdentifier =
                derivationOperations.DeriveAngorKey(projectInvestmentInfo.FounderKey, angorRootKey);
            projectInvestmentInfo.ProjectSeeders = new ProjectSeeders();

            // Create the seeder 1 params
            var seeder11Key = new Key();
            var seeder1ChangeKey = new Key();
            var seeder1ReceiveCoinsKey = new Key();

            ProjectSeeders projectSeeders = new ProjectSeeders();
            projectSeeders.Threshold = 2;
            projectSeeders.SecretHashes.Add(Hashes.Hash256(new Key().ToBytes()).ToString());
            projectSeeders.SecretHashes.Add(Hashes.Hash256(new Key().ToBytes()).ToString());
            projectSeeders.SecretHashes.Add(Hashes.Hash256(new Key().ToBytes()).ToString());

            InvestorContext seeder1Context = new InvestorContext() { ProjectInfo = projectInvestmentInfo };

            seeder1Context.InvestorKey = Encoders.Hex.EncodeData(seeder11Key.PubKey.ToBytes());
            seeder1Context.ChangeAddress = seeder1ChangeKey.PubKey.GetSegwitAddress(network).ToString();

            // create the investment transaction

            var seeder1InvTrx = operations.CreateInvestmentTransaction(network, seeder1Context,
                projectInvestmentInfo.TargetAmount);

            seeder1Context.TransactionHex = operations.SignInvestmentTransaction(network, seeder1Context.ChangeAddress,
                    seeder1InvTrx, null, new List<UtxoDataWithPath>(), _expectedFeeEstimation)
                .ToHex();

            var seeder1Expierytrx = operations.RecoverEndOfProjectFunds(network, seeder1Context, new[] { 2, 3 },
                seeder1ReceiveCoinsKey.PubKey.ScriptPubKey, Encoders.Hex.EncodeData(seeder11Key.ToBytes()));

            Assert.NotNull(seeder1Expierytrx);
        }

        [Fact]
        public void SpendInvestorRecoveryTest()
        {
            DerivationOperations derivationOperations = new DerivationOperations(new HdOperations(),
                new NullLogger<DerivationOperations>(), _networkConfiguration.Object);
            InvestmentOperations operations = new InvestmentOperations(_walletOperations.Object, derivationOperations);

            var network = Networks.Bitcoin.Testnet();

            var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

            // Create the seeder 1 params
            var investorKey = new Key();
            var investorChangeKey = new Key();

            var funderKey = derivationOperations.DeriveFounderKey(words, 1);
            var funderRecoveryKey = derivationOperations.DeriveFounderRecoveryKey(words, 1);
            var angorKey = derivationOperations.DeriveAngorKey(funderKey, angorRootKey);
            var funderPrivateKey = derivationOperations.DeriveFounderPrivateKey(words, 1);
            var funderRecoveryPrivateKey = derivationOperations.DeriveFounderRecoveryPrivateKey(words, 1);

            var investorContext = new InvestorContext
            {
                ProjectInfo = new ProjectInfo
                {
                    TargetAmount = Money.Coins(3).Satoshi,
                    StartDate = DateTime.UtcNow,
                    ExpiryDate = DateTime.UtcNow.AddDays(5),
                    Stages = new List<Stage>
                    {
                        new() { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
                        new() { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(2) },
                        new() { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(3) }
                    },
                    FounderKey = funderKey,
                    FounderRecoveryKey = funderRecoveryKey,
                    ProjectIdentifier = angorKey,
                    PenaltyDays = 5,
                    ProjectSeeders = new ProjectSeeders()
                },
                InvestorKey = Encoders.Hex.EncodeData(investorKey.PubKey.ToBytes()),
                ChangeAddress = investorChangeKey.PubKey.GetSegwitAddress(network).ToString()
            };

            // create the investment transaction

            var investmentTransaction = operations.CreateInvestmentTransaction(network, investorContext,
                investorContext.ProjectInfo.TargetAmount);

            investorContext.TransactionHex = investmentTransaction.ToHex();

            var recoveryTransactions = operations.BuildRecoverInvestorFundsTransactions(investorContext, network,
                Encoders.Hex.EncodeData(investorChangeKey.PubKey.ToBytes()));

            var founderSignatures = operations.FounderSignInvestorRecoveryTransactions(investorContext, network,
                recoveryTransactions,
                Encoders.Hex.EncodeData(funderRecoveryPrivateKey.ToBytes()));

            operations.AddWitScriptToInvestorRecoveryTransactions(investorContext, network, recoveryTransactions,
                founderSignatures, Encoders.Hex.EncodeData(investorKey.ToBytes()), null);

            var nbitcoinNetwork = NetworkMapper.Map(network);

            var builder = nbitcoinNetwork.CreateTransactionBuilder();
            for (int i = 0; i < investmentTransaction.Outputs.Count; i++)
            {
                var output = investmentTransaction.Outputs[i];

                if (output.Value == 0)
                    continue;
                builder.AddCoin(new NBitcoin.Coin(Transaction.Parse(investmentTransaction.ToHex(), nbitcoinNetwork),
                    (uint)i));
                builder.AddCoin(new NBitcoin.Coin(NBitcoin.uint256.Zero, 0, new NBitcoin.Money(1000),
                    new Script(
                        "4a8a3d6bb78a5ec5bf2c599eeb1ea522677c4b10132e554d78abecd7561e4b42"))); //Adding fee inputs
            }

            var parsedTransactions = recoveryTransactions
                .Select(recoveryTransaction =>
                {
                    var trx = NBitcoin.Transaction.Parse(recoveryTransaction.ToHex(), nbitcoinNetwork);

                    trx.Inputs.Add(new OutPoint(NBitcoin.uint256.Zero, 0), null, null); //Add fee to the transaction

                    return trx;
                });

            Assert.All(parsedTransactions, _ =>
            {
                builder.Verify(_, out TransactionPolicyError[] errors);
                Assert.Empty(errors);
            });
        }

        [Fact]
        public void SpendInvestorConsolidatedRecoveryTest()
        {
            {
                DerivationOperations derivationOperations = new DerivationOperations(new HdOperations(),
                    new NullLogger<DerivationOperations>(), _networkConfiguration.Object);
                InvestmentOperations operations =
                    new InvestmentOperations(_walletOperations.Object, derivationOperations);

                var network = Networks.Bitcoin.Testnet();

                var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

                var funderKey = derivationOperations.DeriveFounderKey(words, 1);
                var funderRecoveryKey = derivationOperations.DeriveFounderRecoveryKey(words, 1);
                var angorKey = derivationOperations.DeriveAngorKey(funderKey, angorRootKey);
                var funderPrivateKey = derivationOperations.DeriveFounderPrivateKey(words, 1);
                var funderRecoveryPrivateKey = derivationOperations.DeriveFounderRecoveryPrivateKey(words, 1);

                // Create the investor 1 params
                var investorKey = new Key();
                var investorChangeKey = new Key();

                var seeder1Key = new Key();
                var seeder2Key = new Key();
                var seeder3Key = new Key();

                var investorContext = new InvestorContext
                {
                    ProjectInfo = new ProjectInfo
                    {
                        TargetAmount = Money.Coins(3).Satoshi,
                        StartDate = DateTime.UtcNow,
                        ExpiryDate = DateTime.UtcNow.AddDays(5),
                        Stages = new List<Stage>
                        {
                            new() { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
                            new() { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(2) },
                            new() { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(3) }
                        },
                        FounderKey = funderKey,
                        FounderRecoveryKey = funderRecoveryKey,
                        ProjectIdentifier = angorKey,
                        PenaltyDays = 5,
                        ProjectSeeders = new ProjectSeeders
                        {
                            Threshold = 2,
                            SecretHashes = new List<string>
                            {
                                Hashes.Hash256(seeder1Key.ToBytes()).ToString(),
                                Hashes.Hash256(seeder2Key.ToBytes()).ToString(),
                                Hashes.Hash256(seeder3Key.ToBytes()).ToString(),
                            }
                        }
                    },
                    InvestorKey = Encoders.Hex.EncodeData(investorKey.PubKey.ToBytes()),
                    ChangeAddress = investorChangeKey.PubKey.GetSegwitAddress(network).ToString(),
                };

                // create the investment transaction

                var investmentTransaction = operations.CreateInvestmentTransaction(network, investorContext,
                    investorContext.ProjectInfo.TargetAmount);

                investorContext.TransactionHex = investmentTransaction.ToHex();

                var recoveryTransactions = operations.BuildRecoverInvestorFundsTransactions(investorContext, network,
                    Encoders.Hex.EncodeData(investorChangeKey.PubKey.ToBytes()));

                var founderSignatures = operations.FounderSignInvestorRecoveryTransactions(investorContext, network,
                    recoveryTransactions,
                    Encoders.Hex.EncodeData(funderRecoveryPrivateKey.ToBytes()));

                operations.AddWitScriptToInvestorRecoveryTransactions(investorContext, network, recoveryTransactions,
                    founderSignatures, Encoders.Hex.EncodeData(investorKey.ToBytes()), null);

                var nbitcoinNetwork = NetworkMapper.Map(network);

                var builder = nbitcoinNetwork.CreateTransactionBuilder();
                for (int i = 0; i < investmentTransaction.Outputs.Count; i++)
                {
                    var output = investmentTransaction.Outputs[i];

                    if (output.Value == 0)
                        continue;
                    builder.AddCoin(new NBitcoin.Coin(Transaction.Parse(investmentTransaction.ToHex(), nbitcoinNetwork),
                        (uint)i));
                    builder.AddCoin(new NBitcoin.Coin(NBitcoin.uint256.Zero, 0, new NBitcoin.Money(10000),
                        new Script(
                            "4a8a3d6bb78a5ec5bf2c599eeb1ea522677c4b10132e554d78abecd7561e4b42"))); //Adding fee inputs
                }

                var transaction = nbitcoinNetwork.Consensus.ConsensusFactory.CreateTransaction();

                foreach (var recoveryTransaction in recoveryTransactions)
                {
                    transaction.Inputs.AddRange(recoveryTransaction.Inputs.Select(_ =>
                    {
                        var txIn = new TxIn(new OutPoint(new NBitcoin.uint256(_.PrevOut.Hash.ToBytes()), _.PrevOut.N));
                        txIn.WitScript = new WitScript(_.WitScript.ToBytes());
                        return txIn;
                    }));
                    transaction.Outputs.AddRange(recoveryTransaction.Outputs.Select(_ =>
                        TxOut.Parse(_.ToHex(network.Consensus.ConsensusFactory))));
                    // transaction.Outputs.Add(new NBitcoin.Money(recoveryTransaction.Outputs.Single().Value.Satoshi),
                    //     new Script(recoveryTransaction.Outputs.Single().ScriptPubKey.ToBytes()));
                }

                transaction.Inputs.Add(new OutPoint(NBitcoin.uint256.Zero, 0), null, null); //Add fee to the transaction
                transaction.Outputs.Add(new NBitcoin.Money(9000), new Script(investorChangeKey.ScriptPubKey.ToBytes()));

                builder.Verify(transaction, out TransactionPolicyError[] errors);
                Assert.Empty(errors);
            }
        }

        [Fact]
        public void SpendSeederConsolidatedRecoveryTest()
        {
            {
                DerivationOperations derivationOperations = new DerivationOperations(new HdOperations(),
                    new NullLogger<DerivationOperations>(), _networkConfiguration.Object);
                InvestmentOperations operations =
                    new InvestmentOperations(_walletOperations.Object, derivationOperations);

                var network = Networks.Bitcoin.Testnet();

                var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

                var funderKey = derivationOperations.DeriveFounderKey(words, 1);
                var funderRecoveryKey = derivationOperations.DeriveFounderRecoveryKey(words, 1);
                var angorKey = derivationOperations.DeriveAngorKey(funderKey, angorRootKey);
                var funderPrivateKey = derivationOperations.DeriveFounderPrivateKey(words, 1);
                var funderRecoveryPrivateKey = derivationOperations.DeriveFounderRecoveryPrivateKey(words, 1);

                // Create the investor 1 params
                var seederKey = new Key();
                var seederChangeKey = new Key();
                var seedersecret = new Key();

                var seederContext = new InvestorContext
                {
                    ProjectInfo = new ProjectInfo
                    {
                        TargetAmount = Money.Coins(3).Satoshi,
                        StartDate = DateTime.UtcNow,
                        ExpiryDate = DateTime.UtcNow.AddDays(5),
                        Stages = new List<Stage>
                        {
                            new() { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
                            new() { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(2) },
                            new() { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(3) }
                        },
                        FounderKey = funderKey,
                        FounderRecoveryKey = funderRecoveryKey,
                        ProjectIdentifier = angorKey,
                        PenaltyDays = 5,
                        ProjectSeeders = new ProjectSeeders()
                    },
                    InvestorKey = Encoders.Hex.EncodeData(seederKey.PubKey.ToBytes()),
                    InvestorSecretHash = Hashes.Hash256(seedersecret.ToBytes()).ToString(),
                    ChangeAddress = seederChangeKey.PubKey.GetSegwitAddress(network).ToString()
                };

                // create the investment transaction

                var investmentTransaction = operations.CreateInvestmentTransaction(network, seederContext,
                    seederContext.ProjectInfo.TargetAmount);

                seederContext.TransactionHex = investmentTransaction.ToHex();

                var recoveryTransactions = operations.BuildRecoverInvestorFundsTransactions(seederContext, network,
                    Encoders.Hex.EncodeData(seederChangeKey.PubKey.ToBytes()));

                var founderSignatures = operations.FounderSignInvestorRecoveryTransactions(seederContext, network,
                    recoveryTransactions,
                    Encoders.Hex.EncodeData(funderRecoveryPrivateKey.ToBytes()));

                operations.AddWitScriptToInvestorRecoveryTransactions(seederContext, network, recoveryTransactions,
                    founderSignatures, Encoders.Hex.EncodeData(seederKey.ToBytes()),
                    Encoders.Hex.EncodeData(seedersecret.ToBytes()));

                var nbitcoinNetwork = NetworkMapper.Map(network);

                var builder = nbitcoinNetwork.CreateTransactionBuilder();
                for (int i = 0; i < investmentTransaction.Outputs.Count; i++)
                {
                    var output = investmentTransaction.Outputs[i];

                    if (output.Value == 0)
                        continue;
                    builder.AddCoin(new NBitcoin.Coin(Transaction.Parse(investmentTransaction.ToHex(), nbitcoinNetwork),
                        (uint)i));
                    builder.AddCoin(new NBitcoin.Coin(NBitcoin.uint256.Zero, 0, new NBitcoin.Money(10000),
                        new Script(
                            "4a8a3d6bb78a5ec5bf2c599eeb1ea522677c4b10132e554d78abecd7561e4b42"))); //Adding fee inputs
                }

                var transaction = nbitcoinNetwork.Consensus.ConsensusFactory.CreateTransaction();

                foreach (var recoveryTransaction in recoveryTransactions)
                {
                    transaction.Inputs.AddRange(recoveryTransaction.Inputs.Select(_ =>
                    {
                        var txIn = new TxIn(new OutPoint(new NBitcoin.uint256(_.PrevOut.Hash.ToBytes()), _.PrevOut.N));
                        txIn.WitScript = new WitScript(_.WitScript.ToBytes());
                        return txIn;
                    }));
                    transaction.Outputs.AddRange(recoveryTransaction.Outputs.Select(_ =>
                        TxOut.Parse(_.ToHex(network.Consensus.ConsensusFactory))));
                    // transaction.Outputs.Add(new NBitcoin.Money(recoveryTransaction.Outputs.Single().Value.Satoshi),
                    //     new Script(recoveryTransaction.Outputs.Single().ScriptPubKey.ToBytes()));
                }

                transaction.Inputs.Add(new OutPoint(NBitcoin.uint256.Zero, 0), null, null); //Add fee to the transaction
                transaction.Outputs.Add(new NBitcoin.Money(9000), new Script(seederChangeKey.ScriptPubKey.ToBytes()));

                builder.Verify(transaction, out TransactionPolicyError[] errors);
                Assert.Empty(errors);
            }
        }

        [Fact]
        public void InvestorTransaction_NoPenalty_Test()
        {
            DerivationOperations derivationOperations = new DerivationOperations(new HdOperations(),
                new NullLogger<DerivationOperations>(), _networkConfiguration.Object);
            InvestmentOperations operations = new InvestmentOperations(_walletOperations.Object, derivationOperations);

            var network = Networks.Bitcoin.Testnet();

            var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

            var projectInvestmentInfo = new ProjectInfo();
            projectInvestmentInfo.TargetAmount = Money.Coins(3).Satoshi;
            projectInvestmentInfo.StartDate = DateTime.UtcNow;
            projectInvestmentInfo.ExpiryDate = DateTime.UtcNow.AddDays(5);
            projectInvestmentInfo.Stages = new List<Stage>
            {
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(2) },
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(3) }
            };
            projectInvestmentInfo.FounderKey = derivationOperations.DeriveFounderKey(words, 1);
            projectInvestmentInfo.FounderRecoveryKey = derivationOperations.DeriveFounderRecoveryKey(words, 1);
            projectInvestmentInfo.ProjectIdentifier =
                derivationOperations.DeriveAngorKey(projectInvestmentInfo.FounderKey, angorRootKey);

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

            projectInvestmentInfo.ProjectSeeders = projectSeeders;

            InvestorContext seeder1Context = new InvestorContext() { ProjectInfo = projectInvestmentInfo };

            seeder1Context.InvestorKey = Encoders.Hex.EncodeData(seeder11Key.PubKey.ToBytes());
            seeder1Context.ChangeAddress = seeder1ChangeKey.PubKey.GetSegwitAddress(network).ToString();

            // create the investment transaction

            var seeder1InvTrx = operations.CreateInvestmentTransaction(network, seeder1Context,
                projectInvestmentInfo.TargetAmount);

            seeder1Context.TransactionHex = operations.SignInvestmentTransaction(network, seeder1Context.ChangeAddress,
                    seeder1InvTrx, null, new List<UtxoDataWithPath>(), _expectedFeeEstimation)
                .ToHex();

            var seeder1Expierytrx = operations.RecoverFundsNoPenalty(network, seeder1Context, new[] { 2, 3 },
                new Key[] { seeder2Key, seeder3Key }, seeder1ReceiveCoinsKey.PubKey.ScriptPubKey,
                Encoders.Hex.EncodeData(seeder11Key.ToBytes()));

            Assert.NotNull(seeder1Expierytrx);
        }
    }
}
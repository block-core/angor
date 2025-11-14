using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Protocol;
using Angor.Shared.Protocol.Scripts;
using Angor.Shared.Protocol.TransactionBuilders;
using Angor.Test.DataBuilders;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;
using Blockcore.NBitcoin.DataEncoders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NBitcoin;
using Coin = Blockcore.NBitcoin.Coin;
using Key = Blockcore.NBitcoin.Key;
using Money = Blockcore.NBitcoin.Money;
using Script = Blockcore.Consensus.ScriptInfo.Script;
using uint256 = Blockcore.NBitcoin.uint256;

namespace Angor.Test.Protocol
{
    public class InvestmentIntegrationsTests
    {
        private Mock<IWalletOperations> _walletOperations;
        private Mock<INetworkConfiguration> _networkConfiguration;
        private DerivationOperations _derivationOperations;
        private SeederTransactionActions _seederTransactionActions;
        private InvestorTransactionActions _investorTransactionActions;
        private FounderTransactionActions _founderTransactionActions;

        private string angorRootKey =
            "tpubD8JfN1evVWPoJmLgVg6Usq2HEW9tLqm6CyECAADnH5tyQosrL6NuhpL9X1cQCbSmndVrgLSGGdbRqLfUbE6cRqUbrHtDJgSyQEY2Uu7WwTL";

        private FeeEstimation _expectedFeeEstimation = new FeeEstimation()
        { Confirmations = 1, FeeRate = 10000 };

        public InvestmentIntegrationsTests()
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

            _derivationOperations = new DerivationOperations(new HdOperations(), new NullLogger<DerivationOperations>(), _networkConfiguration.Object);
            _seederTransactionActions = new SeederTransactionActions(new NullLogger<SeederTransactionActions>(),
                new InvestmentScriptBuilder(new SeederScriptTreeBuilder()),
                new ProjectScriptsBuilder(_derivationOperations),
                new SpendingTransactionBuilder(_networkConfiguration.Object,
                    new ProjectScriptsBuilder(_derivationOperations),
                    new InvestmentScriptBuilder(new SeederScriptTreeBuilder())),
                new InvestmentTransactionBuilder(_networkConfiguration.Object,
                    new ProjectScriptsBuilder(_derivationOperations), new InvestmentScriptBuilder(new SeederScriptTreeBuilder()), new TaprootScriptBuilder()),
                new TaprootScriptBuilder(), _networkConfiguration.Object);

            _investorTransactionActions = new InvestorTransactionActions(new NullLogger<InvestorTransactionActions>(),
                new InvestmentScriptBuilder(new SeederScriptTreeBuilder()),
                new ProjectScriptsBuilder(_derivationOperations),
                new SpendingTransactionBuilder(_networkConfiguration.Object,
                    new ProjectScriptsBuilder(_derivationOperations),
                    new InvestmentScriptBuilder(new SeederScriptTreeBuilder())),
                new InvestmentTransactionBuilder(_networkConfiguration.Object,
                    new ProjectScriptsBuilder(_derivationOperations),
                    new InvestmentScriptBuilder(new SeederScriptTreeBuilder()), new TaprootScriptBuilder()),
                new TaprootScriptBuilder(), _networkConfiguration.Object);

            _founderTransactionActions = new FounderTransactionActions(new NullLogger<FounderTransactionActions>(), _networkConfiguration.Object,
                new ProjectScriptsBuilder(_derivationOperations),
                new InvestmentScriptBuilder(new SeederScriptTreeBuilder()), new TaprootScriptBuilder());
        }

        [Fact]
        public void SpendFounderStage_Test()
        {

            var network = Networks.Bitcoin.Testnet();

            var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

            var funderKey = _derivationOperations.DeriveFounderPrivateKey(words, 1);

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
            projectInvestmentInfo.FounderKey = _derivationOperations.DeriveFounderKey(words, 1);
            projectInvestmentInfo.FounderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(words, projectInvestmentInfo.FounderKey);
            projectInvestmentInfo.ProjectIdentifier =
                _derivationOperations.DeriveAngorKey(angorRootKey, projectInvestmentInfo.FounderKey);

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

            var seeder1InvTrx = _seederTransactionActions.CreateInvestmentTransaction(projectInvestmentInfo,
                seeder1Context.InvestorKey, new uint256(seeder1Context.InvestorSecretHash), projectInvestmentInfo.TargetAmount);

            founderContext.InvestmentTrasnactionsHex.Add(seeder1InvTrx.ToHex());

            // create seeder2 investment transaction

            var seeder2InvTrx = _seederTransactionActions.CreateInvestmentTransaction(projectInvestmentInfo,
                seeder2Context.InvestorKey, new uint256(seeder2Context.InvestorSecretHash), projectInvestmentInfo.TargetAmount);

            founderContext.InvestmentTrasnactionsHex.Add(seeder2InvTrx.ToHex());

            // create seeder3 investment transaction

            var seeder3InvTrx = _seederTransactionActions.CreateInvestmentTransaction(projectInvestmentInfo,
                seeder3Context.InvestorKey, new uint256(seeder3Context.InvestorSecretHash), projectInvestmentInfo.TargetAmount);

            founderContext.InvestmentTrasnactionsHex.Add(seeder3InvTrx.ToHex());

            // create investor 1 investment transaction

            var investor1InvTrx = _investorTransactionActions.CreateInvestmentTransaction(projectInvestmentInfo,
                investor1Context.InvestorKey, projectInvestmentInfo.TargetAmount);

            founderContext.InvestmentTrasnactionsHex.Add(investor1InvTrx.ToHex());

            // create investor 2 investment transaction

            var investor2InvTrx = _investorTransactionActions.CreateInvestmentTransaction(projectInvestmentInfo,
                investor2Context.InvestorKey, projectInvestmentInfo.TargetAmount);

            founderContext.InvestmentTrasnactionsHex.Add(investor2InvTrx.ToHex());

            // spend all investment transactions for stage 1

            var founderTrxForSeeder1Stage1 = _founderTransactionActions.SpendFounderStage(projectInvestmentInfo,
                founderContext.InvestmentTrasnactionsHex, 1,
                funderReceiveCoinsKey.PubKey.ScriptPubKey, Encoders.Hex.EncodeData(funderKey.ToBytes())
                , _expectedFeeEstimation);

            TransactionValidation.ThanTheTransactionHasNoErrors(founderTrxForSeeder1Stage1.Transaction,
                founderContext.InvestmentTrasnactionsHex.Select(_ => Networks.Bitcoin.Testnet().CreateTransaction(_))
                    .SelectMany(_ => _.Outputs.AsCoins().Where(c => c.Amount > 0)));
        }

        [Fact]
        public void SeederTransaction_EndOfProject_Test()
        {
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
            projectInvestmentInfo.FounderKey = _derivationOperations.DeriveFounderKey(words, 1);
            projectInvestmentInfo.FounderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(words, projectInvestmentInfo.FounderKey);
            projectInvestmentInfo.ProjectIdentifier =
                _derivationOperations.DeriveAngorKey(angorRootKey, projectInvestmentInfo.FounderKey);

            projectInvestmentInfo.PenaltyDays = 180;

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

            var seeder1InvTrx = _seederTransactionActions.CreateInvestmentTransaction(projectInvestmentInfo, seeder1Context.InvestorKey,
                new uint256(seeder1Context.InvestorSecretHash), projectInvestmentInfo.TargetAmount);

            var seeder1Expierytrx = _seederTransactionActions.RecoverEndOfProjectFunds(seeder1InvTrx.ToHex(), projectInvestmentInfo,
                1, seeder1ReceiveCoinsKey.PubKey.ScriptPubKey.WitHash.GetAddress(network).ToString(),
                Encoders.Hex.EncodeData(seeder11Key.ToBytes()), _expectedFeeEstimation);

            Assert.NotNull(seeder1Expierytrx);

            TransactionValidation.ThanTheTransactionHasNoErrors(seeder1Expierytrx.Transaction,
                seeder1InvTrx.Outputs.AsCoins().Where(c => c.Amount > 0));
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
            projectInvestmentInfo.FounderRecoveryKey = derivationOperations.DeriveFounderRecoveryKey(words, projectInvestmentInfo.FounderKey);
            projectInvestmentInfo.ProjectIdentifier =
                derivationOperations.DeriveAngorKey(angorRootKey, projectInvestmentInfo.FounderKey);
            projectInvestmentInfo.ProjectSeeders = new ProjectSeeders();

            // Create the seeder 1 params
            var seeder11Key = new Key();
            var seeder1ChangeKey = new Key();
            var seeder1ReceiveCoinsKey = new Key();

            InvestorContext seeder1Context = new InvestorContext() { ProjectInfo = projectInvestmentInfo };

            seeder1Context.InvestorKey = Encoders.Hex.EncodeData(seeder11Key.PubKey.ToBytes());
            seeder1Context.ChangeAddress = seeder1ChangeKey.PubKey.GetSegwitAddress(network).ToString();

            // create the investment transaction

            var investorInvTrx = _investorTransactionActions.CreateInvestmentTransaction(projectInvestmentInfo, seeder1Context.InvestorKey,
                projectInvestmentInfo.TargetAmount);

            var investor1Expierytrx = _investorTransactionActions.RecoverEndOfProjectFunds(investorInvTrx.ToHex(),
                projectInvestmentInfo,
                1, seeder1ReceiveCoinsKey.PubKey.ScriptPubKey.WitHash.GetAddress(network).ToString(),
                Encoders.Hex.EncodeData(seeder11Key.ToBytes()), _expectedFeeEstimation);

            Assert.NotNull(investor1Expierytrx);

            TransactionValidation.ThanTheTransactionHasNoErrors(investor1Expierytrx.Transaction,
                investorInvTrx.Outputs.AsCoins().Where(c => c.Amount > 0));
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
            projectInvestmentInfo.FounderRecoveryKey = derivationOperations.DeriveFounderRecoveryKey(words, projectInvestmentInfo.FounderKey);
            projectInvestmentInfo.ProjectIdentifier =
                derivationOperations.DeriveAngorKey(angorRootKey, projectInvestmentInfo.FounderKey);
            projectInvestmentInfo.ProjectSeeders = new ProjectSeeders();

            // Create the seeder 1 params
            var investorKey = new Key();
            var investorChangeKey = new Key();
            var investorReceiveCoinsKey = new Key();

            ProjectSeeders projectSeeders = new ProjectSeeders();
            projectSeeders.Threshold = 2;
            projectSeeders.SecretHashes.Add(Hashes.Hash256(new Key().ToBytes()).ToString());
            projectSeeders.SecretHashes.Add(Hashes.Hash256(new Key().ToBytes()).ToString());
            projectSeeders.SecretHashes.Add(Hashes.Hash256(new Key().ToBytes()).ToString());

            projectInvestmentInfo.ProjectSeeders = projectSeeders;

            var investorPubKey = Encoders.Hex.EncodeData(investorKey.PubKey.ToBytes());

            // create the investment transaction

            var investorInvTrx = _investorTransactionActions.CreateInvestmentTransaction(projectInvestmentInfo, investorPubKey,
                projectInvestmentInfo.TargetAmount);

            var investorExpierytrx = _investorTransactionActions.RecoverEndOfProjectFunds(investorInvTrx.ToHex(),
                projectInvestmentInfo, 1, investorReceiveCoinsKey.PubKey.ScriptPubKey.WitHash.GetAddress(network).ToString(),
                Encoders.Hex.EncodeData(investorKey.ToBytes()), _expectedFeeEstimation);

            Assert.NotNull(investorExpierytrx);

            TransactionValidation.ThanTheTransactionHasNoErrors(investorExpierytrx.Transaction,
                investorInvTrx.Outputs.AsCoins().Where(c => c.Amount > 0));
        }

        [Fact]
        public void SpendSeederRecoveryTest()
        {
            var network = Networks.Bitcoin.Testnet();

            var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

            // Create the seeder 1 params
            var seederKey = new Key();
            var seederChangeKey = new Key();
            var seederFundsRecoveryKey = new Key();
            var seederSecret = new Key();

            var funderKey = _derivationOperations.DeriveFounderKey(words, 1);
            var founderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(words, funderKey);
            var angorKey = _derivationOperations.DeriveAngorKey(angorRootKey, funderKey);
            var funderPrivateKey = _derivationOperations.DeriveFounderPrivateKey(words, 1);
            var founderRecoveryPrivateKey = _derivationOperations.DeriveFounderRecoveryPrivateKey(words, funderKey);

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
                    FounderRecoveryKey = founderRecoveryKey,
                    ProjectIdentifier = angorKey,
                    PenaltyDays = 5,
                    ProjectSeeders = new ProjectSeeders()
                },
                InvestorKey = Encoders.Hex.EncodeData(seederKey.PubKey.ToBytes()),
                ChangeAddress = seederChangeKey.PubKey.GetSegwitAddress(network).ToString()
            };

            // create the investment transaction

            var investmentTransaction = _seederTransactionActions.CreateInvestmentTransaction(investorContext.ProjectInfo, investorContext.InvestorKey,
                Hashes.Hash256(seederSecret.ToBytes()), investorContext.ProjectInfo.TargetAmount);

            investorContext.TransactionHex = investmentTransaction.ToHex();

            var recoveryTransaction = _seederTransactionActions.BuildRecoverSeederFundsTransaction(investorContext.ProjectInfo,
                investmentTransaction,
                investorContext.ProjectInfo.PenaltyDays, Encoders.Hex.EncodeData(seederFundsRecoveryKey.PubKey.ToBytes()));

            var founderSignatures = _founderTransactionActions.SignInvestorRecoveryTransactions(investorContext.ProjectInfo,
                investmentTransaction.ToHex(), recoveryTransaction,
                Encoders.Hex.EncodeData(founderRecoveryPrivateKey.ToBytes()));

            var signedRecoveryTransaction = _seederTransactionActions.AddSignaturesToRecoverSeederFundsTransaction(investorContext.ProjectInfo,
                investmentTransaction, seederFundsRecoveryKey.PubKey.ToHex(),
                founderSignatures, Encoders.Hex.EncodeData(seederKey.ToBytes()), Encoders.Hex.EncodeData(seederSecret.ToBytes()));

            // Adding the input that will be spent as fee 
            signedRecoveryTransaction.Inputs.Add(new Blockcore.Consensus.TransactionInfo.TxIn(new Blockcore.Consensus.TransactionInfo.OutPoint(Blockcore.NBitcoin.uint256.Zero, 0))); //Add fee to the transaction

            TransactionValidation.ThanTheTransactionHasNoErrors(signedRecoveryTransaction, investmentTransaction.Outputs.AsCoins()
                .Where(_ => _.Amount > 0)
                // Adding the coin to spend as fee - so the transaction validation doesn't fail
                .Append(new Coin(uint256.Zero, 0, new Money(1000),
                    new Blockcore.Consensus.ScriptInfo.Script("4a8a3d6bb78a5ec5bf2c599eeb1ea522677c4b10132e554d78abecd7561e4b42"))));
        }

        [Fact]
        public void SpendInvestorRecoveryTest()
        {
            var network = Networks.Bitcoin.Testnet();

            var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

            // Create the seeder 1 params
            var investorKey = new Key();
            var investorChangeKey = new Key();

            var funderKey = _derivationOperations.DeriveFounderKey(words, 1);
            var angorKey = _derivationOperations.DeriveAngorKey(angorRootKey, funderKey);
            var founderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(words, funderKey);
            var funderPrivateKey = _derivationOperations.DeriveFounderPrivateKey(words, 1);
            var founderRecoveryPrivateKey = _derivationOperations.DeriveFounderRecoveryPrivateKey(words, funderKey);

            var investorContext = new InvestorContext
            {
                ProjectInfo = new ProjectInfo
                {
                    TargetAmount = Money.Coins(3).Satoshi,
                    StartDate = DateTime.UtcNow,
                    ExpiryDate = DateTime.UtcNow.AddDays(5),
                    PenaltyDays = 5,
                    Stages = new List<Stage>
                    {
                        new() { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
                        new() { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(2) },
                        new() { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(3) }
                    },
                    FounderKey = funderKey,
                    FounderRecoveryKey = founderRecoveryKey,
                    ProjectIdentifier = angorKey,
                    ProjectSeeders = new ProjectSeeders()
                },
                InvestorKey = Encoders.Hex.EncodeData(investorKey.PubKey.ToBytes()),
                ChangeAddress = investorChangeKey.PubKey.GetSegwitAddress(network).ToString()
            };

            // create the investment transaction

            var investmentTransaction = _investorTransactionActions.CreateInvestmentTransaction(investorContext.ProjectInfo, investorContext.InvestorKey,
                investorContext.ProjectInfo.TargetAmount);

            investorContext.TransactionHex = investmentTransaction.ToHex();

            var recoveryTransaction = _investorTransactionActions.BuildRecoverInvestorFundsTransaction(investorContext.ProjectInfo,
                investmentTransaction);

            var founderSignatures = _founderTransactionActions.SignInvestorRecoveryTransactions(investorContext.ProjectInfo,
                investmentTransaction.ToHex(), recoveryTransaction,
                Encoders.Hex.EncodeData(founderRecoveryPrivateKey.ToBytes()));

            var sigCheckResult = _investorTransactionActions.CheckInvestorRecoverySignatures(investorContext.ProjectInfo, investmentTransaction, founderSignatures);
            Assert.True(sigCheckResult, "failed to validate the founders signatures");

            var signedRecoveryTransaction = _investorTransactionActions.AddSignaturesToRecoverSeederFundsTransaction(investorContext.ProjectInfo,
                investmentTransaction,
                founderSignatures, Encoders.Hex.EncodeData(investorKey.ToBytes()));

            List<Coin> coins = new();
            foreach (var indexedTxOut in investmentTransaction.Outputs.AsIndexedOutputs().Where(w => !w.TxOut.ScriptPubKey.IsUnspendable))
            {
                coins.Add(new Blockcore.NBitcoin.Coin(indexedTxOut));
                coins.Add(new Blockcore.NBitcoin.Coin(Blockcore.NBitcoin.uint256.Zero, 0, new Blockcore.NBitcoin.Money(1000),
                    new Script("4a8a3d6bb78a5ec5bf2c599eeb1ea522677c4b10132e554d78abecd7561e4b42"))); //Adding fee inputs

            }

            signedRecoveryTransaction.Inputs.Add(new Blockcore.Consensus.TransactionInfo.TxIn(
                new Blockcore.Consensus.TransactionInfo.OutPoint(Blockcore.NBitcoin.uint256.Zero, 0), null)); //Add fee to the transaction

            TransactionValidation.ThanTheTransactionHasNoErrors(signedRecoveryTransaction, coins);

            // recover the coins after the penalty
            var releaseTransaction = _investorTransactionActions.BuildAndSignRecoverReleaseFundsTransaction(investorContext.ProjectInfo, investmentTransaction, signedRecoveryTransaction,
                investorContext.ChangeAddress, _expectedFeeEstimation, Encoders.Hex.EncodeData(investorKey.ToBytes()));

            coins = new();
            foreach (var indexedTxOut in signedRecoveryTransaction.Outputs.AsIndexedOutputs())
            {
                coins.Add(new Blockcore.NBitcoin.Coin(indexedTxOut));
            }

            TransactionValidation.ThanTheTransactionHasNoErrors(releaseTransaction.Transaction, coins);

        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void InvestorTransaction_NoPenalty_Test(int stageIndex)
        {
            var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

            // Create the seeder 1 params
            var investorKey = new Key();
            var investorChangeKey = new Key();
            var investorReceiveCoinsKey = new Key();

            var seeder1Key = new Key();
            var seeder2Key = new Key();
            var seeder3Key = new Key();

            var projectInvestmentInfo = new ProjectInfo
            {
                TargetAmount = Money.Coins(3).Satoshi,
                StartDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(5),
                Stages = new List<Stage>
                {
                    new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
                    new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(2) },
                    new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(3) }
                },
                FounderKey = _derivationOperations.DeriveFounderKey(words, 1),
                FounderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(words, _derivationOperations.DeriveFounderKey(words, 1)),
                ProjectSeeders = new()
                {
                    Threshold = 2,
                    SecretHashes = new List<string>()
                    {
                        Hashes.Hash256(seeder1Key.ToBytes()).ToString(),
                        Hashes.Hash256(seeder2Key.ToBytes()).ToString(),
                        Hashes.Hash256(seeder3Key.ToBytes()).ToString()
                    }
                }
            };

            projectInvestmentInfo.ProjectIdentifier =
                _derivationOperations.DeriveAngorKey(angorRootKey, projectInvestmentInfo.FounderKey);

            // create the investment transaction

            var investorInvTrx = _investorTransactionActions.CreateInvestmentTransaction(projectInvestmentInfo,
                Encoders.Hex.EncodeData(investorKey.PubKey.ToBytes()),
                projectInvestmentInfo.TargetAmount);

            var secrets = new List<byte[]>
            {
                seeder1Key.ToBytes(),
                seeder2Key.ToBytes(),
                seeder3Key.ToBytes()
            };

            for (var i = 0; i < secrets.Count; i++)
            {
                var partSecrets = secrets.Where((_, index) => index != i)
                    .ToList();

                var investorRecoverFundsNoPenalty = _investorTransactionActions.RecoverRemainingFundsWithOutPenalty(
                    investorInvTrx.ToHex(), projectInvestmentInfo, stageIndex,
                    investorReceiveCoinsKey.PubKey.ScriptPubKey.WitHash.GetAddress(Networks.Bitcoin.Testnet()).ToString(),
                    Encoders.Hex.EncodeData(investorKey.ToBytes()), _expectedFeeEstimation, partSecrets
                );

                Assert.NotNull(investorRecoverFundsNoPenalty);

                TransactionValidation.ThanTheTransactionHasNoErrors(investorRecoverFundsNoPenalty.Transaction,
                    investorInvTrx.Outputs.AsCoins().Where(c => c.Amount > 0));
            }
        }

        [Fact]
        public void SpendInvestorReleaseTest()
        {
            var network = Networks.Bitcoin.Testnet();

            var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

            // Create the investor params
            var investorKey = new Key();
            var investorChangeKey = new Key();

            var funderKey = _derivationOperations.DeriveFounderKey(words, 1);
            var angorKey = _derivationOperations.DeriveAngorKey(angorRootKey, funderKey);
            var founderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(words, funderKey);
            var funderPrivateKey = _derivationOperations.DeriveFounderPrivateKey(words, 1);
            var founderRecoveryPrivateKey = _derivationOperations.DeriveFounderRecoveryPrivateKey(words, funderKey);

            var investorContext = new InvestorContext
            {
                ProjectInfo = new ProjectInfo
                {
                    TargetAmount = Money.Coins(3).Satoshi,
                    StartDate = DateTime.UtcNow,
                    ExpiryDate = DateTime.UtcNow.AddDays(5),
                    PenaltyDays = 5,
                    Stages = new List<Stage>
                    {
                        new() { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
                        new() { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(2) },
                        new() { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(3) }
                    },
                    FounderKey = funderKey,
                    FounderRecoveryKey = founderRecoveryKey,
                    ProjectIdentifier = angorKey,
                    ProjectSeeders = new ProjectSeeders()
                },
                InvestorKey = Encoders.Hex.EncodeData(investorKey.PubKey.ToBytes()),
                ChangeAddress = investorChangeKey.PubKey.GetSegwitAddress(network).ToString()
            };

            var investorReleaseKey = new Key();
            var investorReleasePubKey = Encoders.Hex.EncodeData(investorReleaseKey.PubKey.ToBytes());

            // Create the investment transaction
            var investmentTransaction = _investorTransactionActions.CreateInvestmentTransaction(investorContext.ProjectInfo, investorContext.InvestorKey,
                investorContext.ProjectInfo.TargetAmount);

            investorContext.TransactionHex = investmentTransaction.ToHex();

            // Build the release transaction
            var releaseTransaction = _investorTransactionActions.BuildUnfundedReleaseInvestorFundsTransaction(investorContext.ProjectInfo, investmentTransaction, investorReleasePubKey);

            // Sign the release transaction
            var founderSignatures = _founderTransactionActions.SignInvestorRecoveryTransactions(investorContext.ProjectInfo,
                investmentTransaction.ToHex(), releaseTransaction,
                Encoders.Hex.EncodeData(founderRecoveryPrivateKey.ToBytes()));

            var signedReleaseTransaction = _investorTransactionActions.AddSignaturesToUnfundedReleaseFundsTransaction(investorContext.ProjectInfo,
                investmentTransaction, founderSignatures, Encoders.Hex.EncodeData(investorKey.ToBytes()), investorReleasePubKey);

            // Validate the signatures
            var sigCheckResult = _investorTransactionActions.CheckInvestorUnfundedReleaseSignatures(investorContext.ProjectInfo, investmentTransaction, founderSignatures, investorReleasePubKey);
            Assert.True(sigCheckResult, "Failed to validate the founder's signatures");

            List<Coin> coins = new();
            foreach (var indexedTxOut in investmentTransaction.Outputs.AsIndexedOutputs().Where(w => !w.TxOut.ScriptPubKey.IsUnspendable))
            {
                coins.Add(new Blockcore.NBitcoin.Coin(indexedTxOut));
                coins.Add(new Blockcore.NBitcoin.Coin(Blockcore.NBitcoin.uint256.Zero, 0, new Blockcore.NBitcoin.Money(1000),
                    new Script("4a8a3d6bb78a5ec5bf2c599eeb1ea522677c4b10132e554d78abecd7561e4b42"))); // Adding fee inputs
            }

            signedReleaseTransaction.Inputs.Add(new Blockcore.Consensus.TransactionInfo.TxIn(
                new Blockcore.Consensus.TransactionInfo.OutPoint(Blockcore.NBitcoin.uint256.Zero, 0), null)); // Add fee to the transaction

            TransactionValidation.ThanTheTransactionHasNoErrors(signedReleaseTransaction, coins);
        }

        [Fact]
        public void InvestorTransaction_EndOfProject_BelowThreshold_Test()
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
            projectInvestmentInfo.FounderRecoveryKey = derivationOperations.DeriveFounderRecoveryKey(words, projectInvestmentInfo.FounderKey);
            projectInvestmentInfo.ProjectIdentifier =
              derivationOperations.DeriveAngorKey(angorRootKey, projectInvestmentInfo.FounderKey);
            projectInvestmentInfo.ProjectSeeders = new ProjectSeeders();

            // Set penalty threshold to 2 BTC - investment will be 1.5 BTC which is below the threshold
            // This should cause GetExpiryDateOverride to return StartDate instead of null (ExpiryDate)
            projectInvestmentInfo.PenaltyThreshold = Money.Coins(2).Satoshi;

            // Create the investor params
            var investorKey = new Key();
            var investorChangeKey = new Key();
            var investorReceiveCoinsKey = new Key();

            InvestorContext investorContext = new InvestorContext() { ProjectInfo = projectInvestmentInfo };

            investorContext.InvestorKey = Encoders.Hex.EncodeData(investorKey.PubKey.ToBytes());
            investorContext.ChangeAddress = investorChangeKey.PubKey.GetSegwitAddress(network).ToString();

            // Create investment transaction with amount BELOW the penalty threshold (1.5 BTC < 2 BTC)
            long investmentAmountBelowThreshold = Money.Coins(1.5m).Satoshi;

            var investorInvTrx = _investorTransactionActions.CreateInvestmentTransaction(projectInvestmentInfo, investorContext.InvestorKey,
             investmentAmountBelowThreshold);

            // Verify the investment is below the threshold
            var isAboveThreshold = _investorTransactionActions.IsInvestmentAbovePenaltyThreshold(projectInvestmentInfo, investorInvTrx);
            Assert.False(isAboveThreshold, "Investment should be below the penalty threshold");

            // Test RecoverEndOfProjectFunds - this should use StartDate as expiry date override
            var investor1Expierytrx = _investorTransactionActions.RecoverEndOfProjectFunds(investorInvTrx.ToHex(),
           projectInvestmentInfo,
             1, investorReceiveCoinsKey.PubKey.ScriptPubKey.WitHash.GetAddress(network).ToString(),
             Encoders.Hex.EncodeData(investorKey.ToBytes()), _expectedFeeEstimation);

            Assert.NotNull(investor1Expierytrx);

            TransactionValidation.ThanTheTransactionHasNoErrors(investor1Expierytrx.Transaction,
               investorInvTrx.Outputs.AsCoins().Where(c => c.Amount > 0));
        }

        [Fact]
        public void InvestorTransaction_EndOfProject_AboveThreshold_Test()
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
            projectInvestmentInfo.FounderRecoveryKey = derivationOperations.DeriveFounderRecoveryKey(words, projectInvestmentInfo.FounderKey);
            projectInvestmentInfo.ProjectIdentifier =
              derivationOperations.DeriveAngorKey(angorRootKey, projectInvestmentInfo.FounderKey);
            projectInvestmentInfo.ProjectSeeders = new ProjectSeeders();

            // Set penalty threshold to 2 BTC - investment will be 1.5 BTC which is below the threshold
            // This should cause GetExpiryDateOverride to return StartDate instead of null (ExpiryDate)
            projectInvestmentInfo.PenaltyThreshold = Money.Coins(2).Satoshi;

            // Create the investor params
            var investorKey = new Key();
            var investorChangeKey = new Key();
            var investorReceiveCoinsKey = new Key();

            InvestorContext investorContext = new InvestorContext() { ProjectInfo = projectInvestmentInfo };

            investorContext.InvestorKey = Encoders.Hex.EncodeData(investorKey.PubKey.ToBytes());
            investorContext.ChangeAddress = investorChangeKey.PubKey.GetSegwitAddress(network).ToString();

            // Create investment transaction with amount ABOVE the penalty threshold (2.1 BTC <= 2 BTC)
            long investmentAmountBelowThreshold = Money.Coins(2.1m).Satoshi;

            var investorInvTrx = _investorTransactionActions.CreateInvestmentTransaction(projectInvestmentInfo, investorContext.InvestorKey,
             investmentAmountBelowThreshold);

            // Verify the investment is below the threshold
            var isAboveThreshold = _investorTransactionActions.IsInvestmentAbovePenaltyThreshold(projectInvestmentInfo, investorInvTrx);
            Assert.True(isAboveThreshold, "Investment should be above the penalty threshold");

            // Test RecoverEndOfProjectFunds - this should use StartDate as expiry date override
            var investor1Expierytrx = _investorTransactionActions.RecoverEndOfProjectFunds(investorInvTrx.ToHex(),
           projectInvestmentInfo,
             1, investorReceiveCoinsKey.PubKey.ScriptPubKey.WitHash.GetAddress(network).ToString(),
             Encoders.Hex.EncodeData(investorKey.ToBytes()), _expectedFeeEstimation);

            Assert.NotNull(investor1Expierytrx);

            TransactionValidation.ThanTheTransactionHasNoErrors(investor1Expierytrx.Transaction,
               investorInvTrx.Outputs.AsCoins().Where(c => c.Amount > 0));
        }



        [Fact]
        public void SpendFundMonthlyBelowAndAboveThreshold()
        {
            // Arrange
            var network = Networks.Bitcoin.Testnet();
            var words = new WalletWords { Words = new Mnemonic(Wordlist.English, WordCount.Twelve).ToString() };

            var founderKey = _derivationOperations.DeriveFounderPrivateKey(words, 1);
            var founderRecoveryPrivateKey = _derivationOperations.DeriveFounderRecoveryPrivateKey(words, _derivationOperations.DeriveFounderKey(words, 1));
            var founderReceiveAddress = new Key().PubKey.ScriptPubKey;

            // Create a Fund project with 3 monthly stages, payout on 15th of each month
            var projectInfo = new ProjectInfo
            {
                Version = 2,
                ProjectType = ProjectType.Fund,
                TargetAmount = 0,
                PenaltyDays = 30,
                PenaltyThreshold = Money.Coins(1).Satoshi, // Set threshold at 1 BTC
                ExpiryDate = DateTime.UtcNow.AddYears(1),
                FounderKey = _derivationOperations.DeriveFounderKey(words, 1),
                FounderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(words, _derivationOperations.DeriveFounderKey(words, 1)),
                ProjectSeeders = new ProjectSeeders(),
                DynamicStagePatterns = new List<DynamicStagePattern>
     {
              new DynamicStagePattern
      {
           PatternId = 0,
     Name = "3-Month Fund (15th of month)",
  Frequency = StageFrequency.Monthly,
        StageCount = 3,
        PayoutDayType = PayoutDayType.SpecificDayOfMonth,
         PayoutDay = 15 // 15th of each month
       }
   }
            };
            projectInfo.ProjectIdentifier = _derivationOperations.DeriveAngorKey(angorRootKey, projectInfo.FounderKey);

            // Create investor 1 - BELOW threshold (0.5 BTC < 1 BTC threshold)
            var investor1Key = new Key();
            var investor1PrivateKey = investor1Key.ToBytes();
            var investor1ReceiveAddress = new Key().PubKey.ScriptPubKey.WitHash.GetAddress(network).ToString();
            var investor1StartDate = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
            var investor1Amount = Money.Coins(0.5m).Satoshi;

            // Create investor 2 - ABOVE threshold (1.5 BTC > 1 BTC threshold)
            var investor2Key = new Key();
            var investor2PrivateKey = investor2Key.ToBytes();
            var investor2ReceiveAddress = new Key().PubKey.ScriptPubKey.WitHash.GetAddress(network).ToString();
            var investor2StartDate = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
            var investor2Amount = Money.Coins(1.5m).Satoshi;

            // Act & Assert

            // Step 1: Create investment transaction for investor 1 (below threshold)
            var investor1Parameters = ProjectParameters.CreateForDynamicProject(
          Encoders.Hex.EncodeData(investor1Key.PubKey.ToBytes()),
      investor1Amount,
        0,
  investor1StartDate);

            var investor1Trx = _investorTransactionActions.CreateInvestmentTransaction(projectInfo, investor1Parameters);
            Assert.NotNull(investor1Trx);

            // Verify investor 1 is below threshold
            var investor1IsAboveThreshold = _investorTransactionActions.IsInvestmentAbovePenaltyThreshold(projectInfo, investor1Trx);
            Assert.False(investor1IsAboveThreshold, "Investor 1 should be below threshold (0.5 BTC < 1 BTC)");

            TransactionValidation.ThanTheTransactionHasNoErrors(investor1Trx,
          new[] { new Coin(uint256.Zero, 0, Money.Coins(20), new Key().ScriptPubKey) });

            // Step 2: Create investment transaction for investor 2 (above threshold)
            var investor2Parameters = ProjectParameters.CreateForDynamicProject(
        Encoders.Hex.EncodeData(investor2Key.PubKey.ToBytes()),
  investor2Amount,
         0,
   investor2StartDate);

            var investor2Trx = _investorTransactionActions.CreateInvestmentTransaction(projectInfo, investor2Parameters);
            Assert.NotNull(investor2Trx);

            // Verify investor 2 is above threshold
            var investor2IsAboveThreshold = _investorTransactionActions.IsInvestmentAbovePenaltyThreshold(projectInfo, investor2Trx);
            Assert.True(investor2IsAboveThreshold, "Investor 2 should be above threshold (1.5 BTC > 1 BTC)");

            TransactionValidation.ThanTheTransactionHasNoErrors(investor2Trx,
             new[] { new Coin(uint256.Zero, 0, Money.Coins(20), new Key().ScriptPubKey) });

            // Step 3: Investor 1 recovers stage 1 immediately (no penalty, no founder approval needed)
            var investor1RecoverStage1 = _investorTransactionActions.RecoverEndOfProjectFunds(
            investor1Trx.ToHex(),
             projectInfo,
              1, // Stage 1
                        investor1ReceiveAddress,
          Encoders.Hex.EncodeData(investor1PrivateKey),
              _expectedFeeEstimation);

            Assert.NotNull(investor1RecoverStage1);
            Assert.NotNull(investor1RecoverStage1.Transaction);
            TransactionValidation.ThanTheTransactionHasNoErrors(investor1RecoverStage1.Transaction,
                investor1Trx.Outputs.AsCoins().Where(c => c.Amount > 0));

            // Step 4: Investor 2 builds recovery transaction (needs founder signatures because above threshold)
            var investor2RecoveryTrx = _investorTransactionActions.BuildRecoverInvestorFundsTransaction(projectInfo, investor2Trx);
            Assert.NotNull(investor2RecoveryTrx);

            // Step 5: Founder signs investor 2's recovery transaction
            var founderSignatures = _founderTransactionActions.SignInvestorRecoveryTransactions(
                   projectInfo,
              investor2Trx.ToHex(),
                        investor2RecoveryTrx,
              Encoders.Hex.EncodeData(founderRecoveryPrivateKey.ToBytes()));

            Assert.NotNull(founderSignatures);
            Assert.NotEmpty(founderSignatures.Signatures);

            // Step 6: Verify founder signatures
            var sigCheckResult = _investorTransactionActions.CheckInvestorRecoverySignatures(projectInfo, investor2Trx, founderSignatures);
            Assert.True(sigCheckResult, "Failed to validate founder signatures for investor 2");

            // Step 7: Investor 2 adds their signature to recovery transaction
            var investor2SignedRecoveryTrx = _investorTransactionActions.AddSignaturesToRecoverSeederFundsTransaction(
           projectInfo,
      investor2Trx,
        founderSignatures,
      Encoders.Hex.EncodeData(investor2PrivateKey));

            Assert.NotNull(investor2SignedRecoveryTrx);

            // Step 8: After penalty period, investor 2 recovers stage 1
            var investor2RecoverStage1 = _investorTransactionActions.BuildAndSignRecoverReleaseFundsTransaction(
       projectInfo,
       investor2Trx,
    investor2SignedRecoveryTrx,
    investor2ReceiveAddress,
           _expectedFeeEstimation,
  Encoders.Hex.EncodeData(investor2PrivateKey));

            Assert.NotNull(investor2RecoverStage1);
            Assert.NotNull(investor2RecoverStage1.Transaction);

            // Step 9: Founder spends stages 2 and 3 from both investments
            var allInvestmentHexes = new[] { investor1Trx.ToHex(), investor2Trx.ToHex() };

            // Founder spends Stage 2
            var founderSpendStage2 = _founderTransactionActions.SpendFounderStage(
     projectInfo,
    allInvestmentHexes,
  2,
 founderReceiveAddress,
        Encoders.Hex.EncodeData(founderKey.ToBytes()),
      _expectedFeeEstimation);

            Assert.NotNull(founderSpendStage2);
            Assert.NotNull(founderSpendStage2.Transaction);
            TransactionValidation.ThanTheTransactionHasNoErrors(founderSpendStage2.Transaction,
           allInvestmentHexes.SelectMany(hex =>
          network.CreateTransaction(hex).Outputs.AsCoins().Where(c => c.Amount > 0)));

            // Founder spends Stage 3
            var founderSpendStage3 = _founderTransactionActions.SpendFounderStage(
                projectInfo,
                allInvestmentHexes,
                 3,
                  founderReceiveAddress,
             Encoders.Hex.EncodeData(founderKey.ToBytes()),
          _expectedFeeEstimation);

            Assert.NotNull(founderSpendStage3);
            Assert.NotNull(founderSpendStage3.Transaction);
            TransactionValidation.ThanTheTransactionHasNoErrors(founderSpendStage3.Transaction,
         allInvestmentHexes.SelectMany(hex =>
             network.CreateTransaction(hex).Outputs.AsCoins().Where(c => c.Amount > 0)));
        }
    }
}
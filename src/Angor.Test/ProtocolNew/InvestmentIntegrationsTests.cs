using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.ProtocolNew;
using Angor.Shared.ProtocolNew.Scripts;
using Angor.Shared.ProtocolNew.TransactionBuilders;
using Angor.Test.DataBuilders;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.Networks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NBitcoin;
using NBitcoin.Policy;
using Coin = Blockcore.NBitcoin.Coin;
using Key = Blockcore.NBitcoin.Key;
using Money = Blockcore.NBitcoin.Money;
using MoneyUnit = NBitcoin.MoneyUnit;
using OutPoint = NBitcoin.OutPoint;
using Script = Blockcore.Consensus.ScriptInfo.Script;
using Transaction = NBitcoin.Transaction;
using uint256 = Blockcore.NBitcoin.uint256;

namespace Angor.Test
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
            projectInvestmentInfo.TargetAmount = 3;
            projectInvestmentInfo.StartDate = DateTime.UtcNow;
            projectInvestmentInfo.ExpiryDate = DateTime.UtcNow.AddDays(5);
            projectInvestmentInfo.Stages = new List<Stage>
            {
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(2) },
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(3) }
            };
            projectInvestmentInfo.FounderKey = _derivationOperations.DeriveFounderKey(words, 1);
            projectInvestmentInfo.FounderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(words, 1);
            projectInvestmentInfo.ProjectIdentifier =
                _derivationOperations.DeriveAngorKey(projectInvestmentInfo.FounderKey, angorRootKey);

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
                seeder1Context.InvestorKey, new uint256(seeder1Context.InvestorSecretHash), Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

            founderContext.InvestmentTrasnactionsHex.Add(seeder1InvTrx.ToHex());

            // create seeder2 investment transaction

            var seeder2InvTrx = _seederTransactionActions.CreateInvestmentTransaction(projectInvestmentInfo,
                seeder2Context.InvestorKey, new uint256(seeder2Context.InvestorSecretHash), Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

            founderContext.InvestmentTrasnactionsHex.Add(seeder2InvTrx.ToHex());

            // create seeder3 investment transaction

            var seeder3InvTrx = _seederTransactionActions.CreateInvestmentTransaction(projectInvestmentInfo,
                seeder3Context.InvestorKey, new uint256(seeder3Context.InvestorSecretHash), Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

            founderContext.InvestmentTrasnactionsHex.Add(seeder3InvTrx.ToHex());

            // create investor 1 investment transaction

            var investor1InvTrx = _investorTransactionActions.CreateInvestmentTransaction(projectInvestmentInfo,
                investor1Context.InvestorKey, Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

            founderContext.InvestmentTrasnactionsHex.Add(investor1InvTrx.ToHex());

            // create investor 2 investment transaction

            var investor2InvTrx = _investorTransactionActions.CreateInvestmentTransaction(projectInvestmentInfo,
                investor2Context.InvestorKey, Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

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
            projectInvestmentInfo.TargetAmount = 3;
            projectInvestmentInfo.StartDate = DateTime.UtcNow;
            projectInvestmentInfo.ExpiryDate = DateTime.UtcNow.AddDays(5);
            projectInvestmentInfo.Stages = new List<Stage>
            {
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(2) },
                new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(3) }
            };
            projectInvestmentInfo.FounderKey = _derivationOperations.DeriveFounderKey(words, 1);
            projectInvestmentInfo.FounderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(words, 1);
            projectInvestmentInfo.ProjectIdentifier =
                _derivationOperations.DeriveAngorKey(projectInvestmentInfo.FounderKey, angorRootKey);

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

            var seeder1InvTrx = _seederTransactionActions.CreateInvestmentTransaction(projectInvestmentInfo,seeder1Context.InvestorKey,
                new uint256(seeder1Context.InvestorSecretHash), Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

            var seeder1Expierytrx  = _seederTransactionActions.RecoverEndOfProjectFunds(seeder1InvTrx.ToHex(), projectInvestmentInfo,
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
            projectInvestmentInfo.TargetAmount = 3;
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

            var investorInvTrx = _investorTransactionActions.CreateInvestmentTransaction(projectInvestmentInfo,seeder1Context.InvestorKey,
                Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

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
            projectInvestmentInfo.TargetAmount = 3;
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

            var investorInvTrx = _investorTransactionActions.CreateInvestmentTransaction(projectInvestmentInfo,investorPubKey,
                Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);

            var investorExpierytrx = _investorTransactionActions.RecoverEndOfProjectFunds(investorInvTrx.ToHex(),
                projectInvestmentInfo, 1, investorReceiveCoinsKey.PubKey.ScriptPubKey.WitHash.GetAddress(network).ToString(),
                Encoders.Hex.EncodeData(investorKey.ToBytes()),_expectedFeeEstimation);

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
            var founderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(words, 1);
            var angorKey = _derivationOperations.DeriveAngorKey(funderKey, angorRootKey);
            var funderPrivateKey = _derivationOperations.DeriveFounderPrivateKey(words, 1);
            var founderRecoveryPrivateKey = _derivationOperations.DeriveFounderRecoveryPrivateKey(words, 1);

            var investorContext = new InvestorContext
            {
                ProjectInfo = new ProjectInfo
                {
                    TargetAmount = 3,
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

            var investmentTransaction = _seederTransactionActions.CreateInvestmentTransaction(investorContext.ProjectInfo,investorContext.InvestorKey,
                Hashes.Hash256(seederSecret.ToBytes()),Money.Coins(investorContext.ProjectInfo.TargetAmount).Satoshi);

            investorContext.TransactionHex = investmentTransaction.ToHex();

            var recoveryTransaction = _seederTransactionActions.BuildRecoverSeederFundsTransaction(investorContext.ProjectInfo, 
                investmentTransaction,
                investorContext.ProjectInfo.PenaltyDays, Encoders.Hex.EncodeData(seederFundsRecoveryKey.PubKey.ToBytes()));

            var founderSignatures = _founderTransactionActions.SignInvestorRecoveryTransactions(investorContext.ProjectInfo,
                investmentTransaction.ToHex(),recoveryTransaction,
                Encoders.Hex.EncodeData(founderRecoveryPrivateKey.ToBytes()));

            var signedRecoveryTransaction = _seederTransactionActions.AddSignaturesToRecoverSeederFundsTransaction(investorContext.ProjectInfo,
                investmentTransaction, seederFundsRecoveryKey.PubKey.ToHex(),
                founderSignatures, Encoders.Hex.EncodeData(seederKey.ToBytes()),Encoders.Hex.EncodeData(seederSecret.ToBytes()));

            // Adding the input that will be spent as fee 
            signedRecoveryTransaction.Inputs.Add(new Blockcore.Consensus.TransactionInfo.TxIn(new Blockcore.Consensus.TransactionInfo.OutPoint(Blockcore.NBitcoin.uint256.Zero,0))); //Add fee to the transaction
            
            TransactionValidation.ThanTheTransactionHasNoErrors(signedRecoveryTransaction,investmentTransaction.Outputs.AsCoins()
                .Where(_ => _.Amount > 0)
                // Adding the coin to spend as fee - so the transaction validation doesn't fail
                .Append(new Coin(uint256.Zero, 0,new Money(1000),
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
            var angorKey = _derivationOperations.DeriveAngorKey(funderKey, angorRootKey);
            var founderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(words, 1);
            var funderPrivateKey = _derivationOperations.DeriveFounderPrivateKey(words, 1);
            var founderRecoveryPrivateKey = _derivationOperations.DeriveFounderRecoveryPrivateKey(words, 1);

            var investorContext = new InvestorContext
            {
                ProjectInfo = new ProjectInfo
                {
                    TargetAmount = 3,
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

            var investmentTransaction = _investorTransactionActions.CreateInvestmentTransaction(investorContext.ProjectInfo,investorContext.InvestorKey,
                Money.Coins(investorContext.ProjectInfo.TargetAmount).Satoshi);

            investorContext.TransactionHex = investmentTransaction.ToHex();

            var recoveryTransaction = _investorTransactionActions.BuildRecoverInvestorFundsTransaction(investorContext.ProjectInfo,
                investmentTransaction);

            var founderSignatures = _founderTransactionActions.SignInvestorRecoveryTransactions(investorContext.ProjectInfo,
                investmentTransaction.ToHex(),recoveryTransaction,
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
                TargetAmount = 3,
                StartDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(5),
                Stages = new List<Stage>
                {
                    new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
                    new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(2) },
                    new Stage { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(3) }
                },
                FounderKey = _derivationOperations.DeriveFounderKey(words, 1),
                FounderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(words, 1),
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
                _derivationOperations.DeriveAngorKey(projectInvestmentInfo.FounderKey, angorRootKey);

            // create the investment transaction

            var investorInvTrx = _investorTransactionActions.CreateInvestmentTransaction(projectInvestmentInfo,
                Encoders.Hex.EncodeData(investorKey.PubKey.ToBytes()),
                Money.Coins(projectInvestmentInfo.TargetAmount).Satoshi);
            
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
                    Encoders.Hex.EncodeData(investorKey.ToBytes()), _expectedFeeEstimation,partSecrets
                );

                Assert.NotNull(investorRecoverFundsNoPenalty);
            
                TransactionValidation.ThanTheTransactionHasNoErrors(investorRecoverFundsNoPenalty.Transaction,
                    investorInvTrx.Outputs.AsCoins().Where(c => c.Amount > 0));
            }
        }
    }
}
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.ProtocolNew;
using Angor.Shared.ProtocolNew.Scripts;
using Angor.Shared.ProtocolNew.TransactionBuilders;
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
using MoneyUnit = NBitcoin.MoneyUnit;
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
            _seederTransactionActions = new SeederTransactionActions(
                new InvestmentScriptBuilder(new SeederScriptTreeBuilder()),
                new ProjectScriptsBuilder(_derivationOperations),
                new SpendingTransactionBuilder(_networkConfiguration.Object,
                    new ProjectScriptsBuilder(_derivationOperations),
                    new InvestmentScriptBuilder(new SeederScriptTreeBuilder())),
                new InvestmentTransactionBuilder(_networkConfiguration.Object,
                    new ProjectScriptsBuilder(_derivationOperations), new InvestmentScriptBuilder(new SeederScriptTreeBuilder())),
                new TaprootScriptBuilder(), _networkConfiguration.Object);

            _investorTransactionActions = new InvestorTransactionActions(
                new InvestmentScriptBuilder(new SeederScriptTreeBuilder()),
                new ProjectScriptsBuilder(_derivationOperations),
                new SpendingTransactionBuilder(_networkConfiguration.Object,
                    new ProjectScriptsBuilder(_derivationOperations),
                    new InvestmentScriptBuilder(new SeederScriptTreeBuilder())),
                new InvestmentTransactionBuilder(_networkConfiguration.Object,
                    new ProjectScriptsBuilder(_derivationOperations),
                    new InvestmentScriptBuilder(new SeederScriptTreeBuilder())),
                new TaprootScriptBuilder(), _networkConfiguration.Object);

            _founderTransactionActions = new FounderTransactionActions(_networkConfiguration.Object,
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

            TransactionValidation.ThanTheTransactionHasNoErrors(founderTrxForSeeder1Stage1,
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
            
            projectInvestmentInfo.PenaltyDate = DateTime.Now.AddMonths(6);

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
                1, seeder1ReceiveCoinsKey.PubKey.ScriptPubKey.ToString(),
                Encoders.Hex.EncodeData(seeder11Key.ToBytes()), _expectedFeeEstimation);

            Assert.NotNull(seeder1Expierytrx);
            
            TransactionValidation.ThanTheTransactionHasNoErrors(seeder1Expierytrx,
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
                1, seeder1ReceiveCoinsKey.PubKey.ScriptPubKey.ToString(),
                Encoders.Hex.EncodeData(seeder11Key.ToBytes()), _expectedFeeEstimation);

            Assert.NotNull(investor1Expierytrx);
            
            TransactionValidation.ThanTheTransactionHasNoErrors(investor1Expierytrx,
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
                projectInvestmentInfo, 1, investorReceiveCoinsKey.PubKey.ScriptPubKey.ToString(),
                Encoders.Hex.EncodeData(investorKey.ToBytes()),_expectedFeeEstimation);

            Assert.NotNull(investorExpierytrx);
            
            TransactionValidation.ThanTheTransactionHasNoErrors(investorExpierytrx,
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
                    PenaltyDate = DateTime.UtcNow.AddDays(5),
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
                investorContext.ProjectInfo.PenaltyDate, Encoders.Hex.EncodeData(seederFundsRecoveryKey.PubKey.ToBytes()));

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
                    Stages = new List<Stage>
                    {
                        new() { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(1) },
                        new() { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(2) },
                        new() { AmountToRelease = 1, ReleaseDate = DateTime.UtcNow.AddDays(3) }
                    },
                    FounderKey = funderKey,
                    FounderRecoveryKey = founderRecoveryKey,
                    ProjectIdentifier = angorKey,
                    PenaltyDate = DateTime.UtcNow.AddDays(5),
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
            
            var parsedTransaction = NBitcoin.Transaction.Parse(signedRecoveryTransaction.ToHex(), nbitcoinNetwork);

            parsedTransaction.Inputs.Add(new OutPoint(NBitcoin.uint256.Zero, 0), null, null); //Add fee to the transaction

            Assert.All(new []{parsedTransaction}, _ =>
            {
                builder.Verify(_, out TransactionPolicyError[] errors);
                Assert.Empty(errors);
            });
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
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
                    investorReceiveCoinsKey.PubKey.ScriptPubKey.ToString(),
                    Encoders.Hex.EncodeData(investorKey.ToBytes()), _expectedFeeEstimation,partSecrets
                );

                Assert.NotNull(investorRecoverFundsNoPenalty);
            
                TransactionValidation.ThanTheTransactionHasNoErrors(investorRecoverFundsNoPenalty,
                    investorInvTrx.Outputs.AsCoins().Where(c => c.Amount > 0));
            }
        }

        [Fact]
        public void TestLive()
        {
            var network = Networks.Bitcoin.Testnet();
            var nbitcoinNetwork = NetworkMapper.Map(network);

            string signedRecoveryTransaction = "01000000000103eb29b9500bae199fe7be415f74256fed56bfa9e945101639261fed43a08abdd10300000000ffffffffeb29b9500bae199fe7be415f74256fed56bfa9e945101639261fed43a08abdd10400000000ffffffff1b73cd0fa4374c29f4b8bedd35c6ca6099b3eb80d1bdb572f0912b52335a99f50500000000ffffffff030087930300000000220020d30c485f5b44be2bfc561f6174e33431ea467437d93a83d790cbbab80da750de000e270700000000220020d30c485f5b44be2bfc561f6174e33431ea467437d93a83d790cbbab80da750de0a4ba71200000000160014c469fa92427e6da0c05294c6fe1b47d68fecf1db0441fbd6a31d93776a9cae7fd8647d6d99ecbcec3536e030bdcdb39ffaf1fd76360ab4ca6574fd4f801cbc1b43ddc4399f4cd144a3f7f25d8e544a33920d02b5d51a8341e44a251dcca10d3e96c1b2a11fa2375b6ac0693a66f9225b80ddf14506afa3e30f03f6e0d52aa0ca2d44712868b807e095a1593945d58ba96b7d265eeaf0cf8b834420ba1295f2b8a82ef5f6cf28c987788b0a0fb9e8dd6bfa46373328d2b947e0ef52ad20459ca76a90a354c2a7026e0f8db0a3457a47521855adcdee2ece6f2a4c6c33c4ac61c09f9aa7a903393a2d6aa6aa744355a25175b7ce04fcd081f04d10802bc90d1003e6055c43b465f7885e3a9a2f14230d469718057c2872c74902d34103f2fbd314f53c9229fa351ac176cd8cabc957072a890d1579e7e67ed190517f18b01b3e4a04410aef28adc3507804eaadaa0c86e1febbf1fe001cd5ea59299d4db41c295c57d949a7eda695a862f83af317b7ae6e81e20de893eff72c5e708d9706571d454118834197bc123193f5d1226c25fd2eeee386a0656e8ee4274b22e0022844ae210bcd058120309432dc7d3e29a5b7ee2c497a422e830a02d655ac895e75f81284d62fda834420ba1295f2b8a82ef5f6cf28c987788b0a0fb9e8dd6bfa46373328d2b947e0ef52ad20459ca76a90a354c2a7026e0f8db0a3457a47521855adcdee2ece6f2a4c6c33c4ac61c19f9aa7a903393a2d6aa6aa744355a25175b7ce04fcd081f04d10802bc90d1003e6055c43b465f7885e3a9a2f14230d469718057c2872c74902d34103f2fbd3142a3f1c6e76d81fe8ff297abc45b697feff2f37fb42d69c9713ced9a49801193c02483045022100c8253cee1b64ef8db4efa2c6f79ffbe2fdeba2f05e4167aa484b00c5dadc82dc02200b10ae34cc9158204a313a69eb708e562935048809456612d92a0be88a37dea0012103f8aca8b4508b117af0a201a46cc6127dab155b62a3cc80f3c3b2fcec770687f500000000";

            var parsedTransaction = NBitcoin.Transaction.Parse(signedRecoveryTransaction, nbitcoinNetwork);

            var builder = nbitcoinNetwork.CreateTransactionBuilder();

            builder.AddCoin(new NBitcoin.Coin(NBitcoin.uint256.Parse("d1bd8aa043ed1f2639161045e9a9bf56ed6f25745f41bee79f19ae0b50b929eb"), 3, new NBitcoin.Money(60000000, MoneyUnit.Satoshi),
                Script.FromHex("51201ae0d87eaa4d590042454c36c04c86298e1f64786284d1eca5a36dc1492351e9")));

            builder.AddCoin(new NBitcoin.Coin(NBitcoin.uint256.Parse("d1bd8aa043ed1f2639161045e9a9bf56ed6f25745f41bee79f19ae0b50b929eb"), 4, new NBitcoin.Money(120000000, MoneyUnit.Satoshi),
                Script.FromHex("5120d5cf9377d8d23eb3ad99c521bebe17b17125f0624556590291f313cce608818d")));

            builder.AddCoin(new NBitcoin.Coin(NBitcoin.uint256.Parse("f5995a33522b91f072b5bdd180ebb39960cac635ddbeb8f4294c37a40fcd731b"), 5, new NBitcoin.Money(312957210, MoneyUnit.Satoshi),
                Script.FromHex("0014c469fa92427e6da0c05294c6fe1b47d68fecf1db")));

            Assert.All(new[] { parsedTransaction }, _ =>
            {
                builder.Verify(_, out TransactionPolicyError[] errors);
                Assert.Empty(errors);
            });
        }
    }
}
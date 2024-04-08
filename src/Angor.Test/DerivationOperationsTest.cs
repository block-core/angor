using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP32;
using Blockcore.NBitcoin.BIP39;
using Blockcore.NBitcoin.Crypto;
using Blockcore.Networks;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace Angor.Test
{
    public class ScriptTDerivationOperationsTestest
    {
        

        private readonly Mock<INetworkConfiguration> _networkConfiguration;
        private readonly ILogger<DerivationOperations> _logger;

        private FeeEstimation _expectedFeeEstimation = new FeeEstimation()
            { Confirmations = 1, FeeRate = 10000 };

        public ScriptTDerivationOperationsTestest()
        {
            _networkConfiguration = new Mock<INetworkConfiguration>();

            _networkConfiguration.Setup(_ => _.GetNetwork())
                .Returns(Networks.Bitcoin.Testnet());

            var mockLogger = new Mock<ILogger<DerivationOperations>>();
            _logger = mockLogger.Object;

        }

        [Fact]
        public void BuildKeys()
        {
            DerivationOperations derivationOperations = new DerivationOperations(new HdOperations(), null, _networkConfiguration.Object);

            var rootKey = CreateAngorRootKey();

            var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);

            var founderKey = derivationOperations.DeriveFounderKey(new WalletWords { Words = mnemonic.ToString() },1);

            var projectId =  derivationOperations.DeriveProjectId(founderKey);

            var angorKey = derivationOperations.DeriveAngorKey(founderKey, rootKey);

            var script = derivationOperations.AngorKeyToScript(angorKey);
        }

        [Fact]
        public void BuildKeysFromExisitData()
        {
            DerivationOperations derivationOperations = new DerivationOperations( new HdOperations(), null, _networkConfiguration.Object);

            var rootKey = CreateAngorRootKey("area frost rapid guitar salon tower bless fly where inmate trouble daughter");

            Assert.Equal("tpubD8JfN1evVWPoJmLgVg6Usq2HEW9tLqm6CyECAADnH5tyQosrL6NuhpL9X1cQCbSmndVrgLSGGdbRqLfUbE6cRqUbrHtDJgSyQEY2Uu7WwTL", rootKey);

            string words = "gospel awkward uphold orchard spike elite inform danger sheriff lens power monitor";

            var founderKey = derivationOperations.DeriveFounderKey(new WalletWords { Words = words }, 1);

            Assert.Equal("030d1351b9828b423a3a63ac7bd04c4d5b15b77be055300e166ddf65515eda905a", founderKey);

            var founderRecoveryKey = derivationOperations.DeriveFounderRecoveryKey(new WalletWords { Words = words }, 1);

            Assert.Equal("02d966f1fd78fad5ae7a6620eaecbacea36223f27ac0c232bb40bbec75f7e5cd42", founderRecoveryKey);

            var projectId = derivationOperations.DeriveProjectId(founderKey);

            Assert.Equal((uint)524503883, projectId);

            var angorKey = derivationOperations.DeriveAngorKey(founderKey, rootKey);

            Assert.Equal("angor1qg07z28wnv3vscsf3qpn4m4ak66zf8anzp38qz2", angorKey);

            var script = derivationOperations.AngorKeyToScript(angorKey);

            Assert.Equal(script, new Script("0 43fc251dd364590c413100675dd7b6d68493f662"));
        }

        private static string CreateAngorRootKey(string words = null)
        {
            ExtKey.UseBCForHMACSHA512 = true;
            Blockcore.NBitcoin.Crypto.Hashes.UseBCForHMACSHA512 = true;

            var mnemonic = words != null ? new Mnemonic(words) :  new Mnemonic(Wordlist.English, WordCount.Twelve);
            
            var extkey = new HdOperations().GetExtendedKey(mnemonic.ToString(), "");

            var path = $"m/5'";

            var pubkey = extkey.Derive(new KeyPath(path)).Neuter();

            var angorRootKey = pubkey.ToString(Networks.Bitcoin.Testnet());

            return angorRootKey;
        }

        [Fact]
        public void DeriveFounderKey_InvalidMnemonicWords_ThrowsException()
        {
            // Arrange
            var derivationOperations = new DerivationOperations(new HdOperations(), _logger, _networkConfiguration.Object);
            var invalidMnemonicWords = "invalid mnemonic words";
            // Act
            Action act = () => derivationOperations.DeriveFounderKey(new WalletWords { Words = invalidMnemonicWords }, 1);
            // Assert
            act.Should().Throw<Exception>().WithMessage("Please make sure you enter valid mnemonic words.");
        }

        [Fact]
        public void GetProjectKey_ExceedKeysDerivationLimit_ThrowsException()
        {
            var derivationOperations = new DerivationOperations(new HdOperations(), null, _networkConfiguration.Object);
            var founderKeyCollection = new FounderKeyCollection();
            
            Action act = () => derivationOperations.GetProjectKey(founderKeyCollection, 16);
            
            act.Should().Throw<Exception>().WithMessage("Keys derivation limit exceeded");
        }

         [Fact]
        public void GetProjectKey_KeyExists_ReturnsArrayOfKeys()
        {
            var founderKeyCollection = new FounderKeyCollection();
            founderKeyCollection.Keys.Add(new FounderKeys { Index = 1 });

            var derivationOperations = new DerivationOperations(new HdOperations(), null, _networkConfiguration.Object);

            var result1 = derivationOperations.GetProjectKey(founderKeyCollection, 1);

            result1.Should().NotBeNull();
            result1.Should().BeOfType<FounderKeys>();

            founderKeyCollection.Keys.Add(new FounderKeys { Index = 2 });

            var result2 = derivationOperations.GetProjectKey(founderKeyCollection, 2);

            result2.Should().NotBeNull();
            result2.Should().BeOfType<FounderKeys>();
        }


    }
}

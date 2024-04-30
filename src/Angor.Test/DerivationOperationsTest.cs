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
    using System;
    using Blockcore.NBitcoin;
    using Blockcore.NBitcoin.DataEncoders;
    using Blockcore.Networks;
    using FluentAssertions;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    
        /// <summary>
        /// The `ScriptTDerivationOperationsTestest` class is a unit test class that tests the functionality of the `DerivationOperations` class.
        /// </summary>
        public class ScriptTDerivationOperationsTestest
        {
            private readonly Mock<INetworkConfiguration> _networkConfiguration;
            private readonly ILogger<DerivationOperations> _logger;

            private FeeEstimation _expectedFeeEstimation = new FeeEstimation()
            {
                Confirmations = 1,
                FeeRate = 10000
            };

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

                var derivationOperations = new DerivationOperations(new HdOperations(), null, _networkConfiguration.Object);
                var rootKey = CreateAngorRootKey();
                var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);


                var founderKey = derivationOperations.DeriveFounderKey(new WalletWords { Words = mnemonic.ToString() }, 1);
                var projectId = derivationOperations.DeriveProjectId(founderKey);
                var angorKey = derivationOperations.DeriveAngorKey(founderKey, rootKey);
                var script = derivationOperations.AngorKeyToScript(angorKey);
            }

            [Fact]
            public void BuildKeysFromExisitData()
            {
                var derivationOperations = new DerivationOperations(new HdOperations(), null, _networkConfiguration.Object);
                var rootKey = CreateAngorRootKey("area frost rapid guitar salon tower bless fly where inmate trouble daughter");


                var words = "gospel awkward uphold orchard spike elite inform danger sheriff lens power monitor";
                var founderKey = derivationOperations.DeriveFounderKey(new WalletWords { Words = words }, 1);
                var founderRecoveryKey = derivationOperations.DeriveFounderRecoveryKey(new WalletWords { Words = words }, 1);
                var projectId = derivationOperations.DeriveProjectId(founderKey);
                var angorKey = derivationOperations.DeriveAngorKey(founderKey, rootKey);
                var script = derivationOperations.AngorKeyToScript(angorKey);

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
                // Arrange
                var derivationOperations = new DerivationOperations(new HdOperations(), null, _networkConfiguration.Object);
                var founderKeyCollection = new FounderKeyCollection();

                // Act
                Action act = () => derivationOperations.GetProjectKey(founderKeyCollection, 16);

                // Assert
                act.Should().Throw<Exception>().WithMessage("Keys derivation limit exceeded");
            }

            [Fact]
            public void GetProjectKey_KeyExists_ReturnsArrayOfKeys()
            {
                // Arrange
                var founderKeyCollection = new FounderKeyCollection();
                founderKeyCollection.Keys.Add(new FounderKeys { Index = 1 });
                var derivationOperations = new DerivationOperations(new HdOperations(), null, _networkConfiguration.Object);

                // Act
                var result1 = derivationOperations.GetProjectKey(founderKeyCollection, 1);
                founderKeyCollection.Keys.Add(new FounderKeys { Index = 2 });
                var result2 = derivationOperations.GetProjectKey(founderKeyCollection, 2);

                // Assert
                result1.Should().NotBeNull().And.BeOfType<FounderKeys>();
                result2.Should().NotBeNull().And.BeOfType<FounderKeys>();
            }

            private static string CreateAngorRootKey(string words = null)
            {
                ExtKey.UseBCForHMACSHA512 = true;
                Blockcore.NBitcoin.Crypto.Hashes.UseBCForHMACSHA512 = true;

                var mnemonic = words != null ? new Mnemonic(words) : new Mnemonic(Wordlist.English, WordCount.Twelve);
                var extkey = new HdOperations().GetExtendedKey(mnemonic.ToString(), "");
                var path = $"m/5'";
                var pubkey = extkey.Derive(new KeyPath(path)).Neuter();
                var angorRootKey = pubkey.ToString(Networks.Bitcoin.Testnet());

                return angorRootKey;
            }
        
    }
}

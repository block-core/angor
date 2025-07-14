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
                
                _networkConfiguration.Setup(_ => _.GetAngorKey())
                    .Returns("tpubD8JfN1evVWPoJmLgVg6Usq2HEW9tLqm6CyECAADnH5tyQosrL6NuhpL9X1cQCbSmndVrgLSGGdbRqLfUbE6cRqUbrHtDJgSyQEY2Uu7WwTL");

                var mockLogger = new Mock<ILogger<DerivationOperations>>();
                _logger = mockLogger.Object;
            }

            [Fact]
            public void BuildKeys()
            {

                var derivationOperations = new DerivationOperations(new HdOperations(), _logger, _networkConfiguration.Object);
                var rootKey = CreateAngorRootKey();
                var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);


                var founderKey = derivationOperations.DeriveFounderKey(new WalletWords { Words = mnemonic.ToString() }, 1);
                var projectId = derivationOperations.DeriveUniqueProjectIdentifier(founderKey);
                var angorKey = derivationOperations.DeriveAngorKey(rootKey, founderKey);
                var script = derivationOperations.AngorKeyToScript(angorKey);
            }

            [Fact]
            public void BuildKeysFromExisitData()
            {
                var derivationOperations = new DerivationOperations(new HdOperations(), _logger, _networkConfiguration.Object);
                var rootKey = CreateAngorRootKey("area frost rapid guitar salon tower bless fly where inmate trouble daughter");

                Assert.Equal("tpubD8JfN1evVWPoJmLgVg6Usq2HEW9tLqm6CyECAADnH5tyQosrL6NuhpL9X1cQCbSmndVrgLSGGdbRqLfUbE6cRqUbrHtDJgSyQEY2Uu7WwTL", rootKey);

            var words = "gospel awkward uphold orchard spike elite inform danger sheriff lens power monitor";
                var founderKey = derivationOperations.DeriveFounderKey(new WalletWords { Words = words }, 1);
                var founderRecoveryKey = derivationOperations.DeriveFounderRecoveryKey(new WalletWords { Words = words }, founderKey);
                var projectId = derivationOperations.DeriveUniqueProjectIdentifier(founderKey);
                var angorKey = derivationOperations.DeriveAngorKey(rootKey, founderKey);
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
            
            
            
            [Fact]
            public void DeriveProjectKeys_ReturnsExpectedKeys()
            {
                // Arrange
                var derivationOperations = new DerivationOperations(new HdOperations(), _logger, _networkConfiguration.Object);
                var words = "gospel awkward uphold orchard spike elite inform danger sheriff lens power monitor";
                var walletWords = new WalletWords { Words = words };

                // Act
                FounderKeyCollection result = derivationOperations.DeriveProjectKeys(walletWords, _networkConfiguration.Object.GetAngorKey());

                // Assert
                result.Should().NotBeNull();
                result.Keys.Should().HaveCount(15); 
                foreach (var keys in result.Keys)
                {
                    keys.ProjectIdentifier.Should().NotBeNullOrEmpty();
                    keys.FounderRecoveryKey.Should().NotBeNullOrEmpty();
                    keys.FounderKey.Should().NotBeNullOrEmpty();
                    keys.NostrPubKey.Should().NotBeNullOrEmpty();
                    keys.Index.Should().BeInRange(1, 15); 
                }
            }
            
            [Fact]
            public void DeriveProjectKeys_InvalidWalletWords_ThrowsException()
            {
                // Arrange
                var derivationOperations = new DerivationOperations(new HdOperations(), _logger, _networkConfiguration.Object);
                WalletWords invalidWalletWords = null; 

                // Act
                Action act = () => derivationOperations.DeriveProjectKeys(invalidWalletWords, _networkConfiguration.Object.GetAngorKey());

                // Assert
                act.Should().Throw<NullReferenceException>(); 
            }
            
            [Fact]
            public void DeriveProjectKeys_InvalidAngorKey_ThrowsException()
            {
                // Arrange
                var derivationOperations = new DerivationOperations(new HdOperations(), _logger, _networkConfiguration.Object);
                var words = "gospel awkward uphold orchard spike elite inform danger sheriff lens power monitor";
                var walletWords = new WalletWords { Words = words };
                string invalidAngorKey = null; 

                // Act
                Action act = () => derivationOperations.DeriveProjectKeys(walletWords, invalidAngorKey);

                // Assert
                act.Should().Throw<ArgumentNullException>(); 
            }
            
            [Fact]
            public void DeriveProjectKeys_NetworkConfigurationFailure_ThrowsException()
            {
                // Arrange
                var invalidNetworkConfiguration = new Mock<INetworkConfiguration>();
                invalidNetworkConfiguration.Setup(_ => _.GetAngorKey()).Throws<Exception>(); 

                var derivationOperations = new DerivationOperations(new HdOperations(), _logger, invalidNetworkConfiguration.Object);
                var words = "gospel awkward uphold orchard spike elite inform danger sheriff lens power monitor";
                var walletWords = new WalletWords { Words = words };
                var angorTestKey = _networkConfiguration.Object.GetAngorKey();

                // Act
                Action act = () => derivationOperations.DeriveProjectKeys(walletWords, angorTestKey);

                // Assert
                act.Should().Throw<Exception>(); 
            }
            
            [Fact]
            public async Task DeriveProjectNostrPrivateKeyAsync_ConcurrencyTest_ReturnsPrivateKey()
            {
                // Arrange
                var derivationOperations = new DerivationOperations(new HdOperations(), _logger, _networkConfiguration.Object);
                var words = "gospel awkward uphold orchard spike elite inform danger sheriff lens power monitor";
                var walletWords = new WalletWords { Words = words };

                // Act
                var tasks = Enumerable.Range(1, 10)
                    .Select(i => derivationOperations.DeriveProjectNostrPrivateKeyAsync(walletWords, new Key().PubKey.ToHex()));

                var results = await Task.WhenAll(tasks);

                // Assert
                results.Should().NotBeNull().And.NotContainNulls();
                results.Should().OnlyContain(r => r is Key);
            }
            
            [Fact]
            public void DeriveAngorKey_TestnetConfiguration_ReturnsValidKey()
            {
                // Arrange
                var mockLogger = new Mock<ILogger<DerivationOperations>>();
                var mockNetworkConfig = new Mock<INetworkConfiguration>();
                mockNetworkConfig.Setup(_ => _.GetNetwork()).Returns(Networks.Bitcoin.Testnet());
                
                var words = "gospel awkward uphold orchard spike elite inform danger sheriff lens power monitor";
                var derivationOperations = new DerivationOperations(new HdOperations(), mockLogger.Object, mockNetworkConfig.Object);
                var founderKey = derivationOperations.DeriveFounderKey(new WalletWords { Words = words }, 1);
                var angorRootKey = CreateAngorRootKey("area frost rapid guitar salon tower bless fly where inmate trouble daughter");

                // Act
                var result = derivationOperations.DeriveAngorKey(angorRootKey, founderKey);

                // Assert
                result.Should().NotBeNull();
                result.Should().BeOfType<string>();
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

            //[Fact]
            public void ValidateAngorMainnetRootKey()
            {
                ExtKey.UseBCForHMACSHA512 = true;
                Blockcore.NBitcoin.Crypto.Hashes.UseBCForHMACSHA512 = true;

                var words = ""; // angro mainnet words
                var passphrase = ""; // the passphrase
                var mnemonic = new Mnemonic(words);
                var extkey = new HdOperations().GetExtendedKey(mnemonic.ToString(), passphrase);
                var path = $"m";
                var pubkey = extkey.Derive(new KeyPath(path)).Neuter();
                var angorRootKey = pubkey.ToString(Networks.Bitcoin.Mainnet());

                Assert.Equal("xpub661MyMwAqRbcGNxKe9aFkPisf3h32gHLJm8f9XAqx8FB1Nk6KngCY8hkhGqxFr2Gyb6yfUaQVbodxLoC1f3K5HU9LM1CXE59gkEXSGCCZ1B", angorRootKey);
            }
        }
    }

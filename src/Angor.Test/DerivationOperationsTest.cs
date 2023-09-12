using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Protocol;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP32;
using Blockcore.NBitcoin.BIP39;
using Blockcore.NBitcoin.Crypto;
using Blockcore.Networks;
using Moq;

namespace Angor.Test
{
    public class ScriptTDerivationOperationsTestest
    {

        private Mock<INetworkConfiguration> _networkConfiguration;

        private FeeEstimation _expectedFeeEstimation = new FeeEstimation()
            { Confirmations = 1, FeeRate = 10000 };

        public ScriptTDerivationOperationsTestest()
        {
            _networkConfiguration = new Mock<INetworkConfiguration>();

            _networkConfiguration.Setup(_ => _.GetNetwork())
                .Returns(Networks.Bitcoin.Testnet());
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

            Assert.Equal("030d1351b9828b423a3a63ac7bd04c4d5b15b77be055300e166ddf65515eda905a", founderRecoveryKey);

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

    }
}

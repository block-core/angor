using NBitcoin;
using NBitcoin.Secp256k1;

namespace Angor.Shared.Tests
{
    public class ProjectIdentifierDerivationTests
    {
        //[Fact]
        public void TestComputeSharedSecretMethods()
        {
            // Arrange
            var privKey1 = new Key();
            var privKey2 = new Key();
            var ecPrivKey1 = ECPrivKey.Create(privKey1.ToBytes());
            var ecPrivKey2 = ECPrivKey.Create(privKey2.ToBytes());
            var ecPubKey1 = ecPrivKey1.CreatePubKey();
            var ecPubKey2 = ecPrivKey2.CreatePubKey();

            // Act
            var sharedSecretFounder = ProjectIdentifierDerivation.ComputeSharedSecretPublicKeySender(ecPrivKey1, ecPubKey2);
            var sharedSecretAngor = ProjectIdentifierDerivation.ComputeSharedSecretPrivateKeyReceiver(ecPubKey1, ecPrivKey2);

            // Assert
            Assert.Equal(sharedSecretFounder.ToBytes(), sharedSecretAngor.CreatePubKey().ToBytes());
        }
    }
}
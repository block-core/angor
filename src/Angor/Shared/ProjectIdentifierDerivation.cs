using NBitcoin.Crypto;
using NBitcoin.Secp256k1;

namespace Angor.Shared;

/// <summary>
/// This class is used to derive a shared secret between two parties.
/// Using the shared secret, the parties can derive a new public key that is unique to the two parties.
/// But only the receiver can derive the private key.
/// Based on this issue https://github.com/block-core/angor/issues/172#issuecomment-2513031390
/// </summary>
public class ProjectIdentifierDerivation
{
    public static ECPubKey ComputeSharedSecretPublicKeySender(ECPrivKey a, ECPubKey B)
    {
        // Let P = B + hash(a·B)·G

        ECPubKey sharedSecret = new ECPubKey(B.GetSharedPubkey(a).Q, null);
        ECPrivKey hashedSharedSecret = ECPrivKey.Create(Hashes.SHA256(sharedSecret.ToBytes()));
        GEJ pmk = hashedSharedSecret.CreatePubKey().Q.ToGroupElementJacobian() + B.Q;
        ECPubKey publicKeyIdentifier = new ECPubKey(pmk.ToGroupElement(), null);

        return publicKeyIdentifier;
    }

    public static ECPrivKey ComputeSharedSecretPrivateKeyReceiver(ECPubKey A, ECPrivKey b)
    {
        // Let p = b + hash(A·b) : note that to get the P one must multiply p with G => p*G

        ECPubKey sharedSecret = new ECPubKey(A.GetSharedPubkey(b).Q, null);
        ECPrivKey hashedSharedSecret = ECPrivKey.Create(Hashes.SHA256(sharedSecret.ToBytes()));
        ECPrivKey privateKeyIdentifier = ECPrivKey.Create((hashedSharedSecret.sec + b.sec).ToBytes());

        return privateKeyIdentifier;
    }
}

using System.Threading.Tasks;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;
using Blockcore.NBitcoin.DataEncoders;
using Microsoft.JSInterop;
using NBitcoin.Crypto;

namespace Angor.Client.Services
{
    public class EncryptionService : IEncryptionService
    {
        private readonly IJSRuntime _js;

        public EncryptionService(IJSRuntime jsRuntime)
        {
            _js = jsRuntime;
        }

        // General-purpose encrypt/decrypt (AES in your JS)
        public Task<string> EncryptData(string secretData, string password)
            => _js.InvokeAsync<string>("encryptData", secretData, password).AsTask();

        public Task<string> DecryptData(string encryptedData, string password)
            => _js.InvokeAsync<string>("decryptData", encryptedData, password).AsTask();

        // Nostr‑DM encrypt/decrypt using your new NIP‑57 (or NIP‑17/44/59) helpers
        public Task<string> EncryptNostrContentAsync(string nsec, string npub, string content)
        {
            var sharedSecret = GetSharedSecretHex(nsec, npub);
            return _js.InvokeAsync<string>("nip57Encrypt", sharedSecret, content).AsTask();
        }

        public Task<string> DecryptNostrContentAsync(string nsec, string npub, string encryptedContent)
        {
            var sharedSecret = GetSharedSecretHex(nsec, npub);
            return _js.InvokeAsync<string>("nip57Decrypt", sharedSecret, encryptedContent).AsTask();
        }

        // Derive the 32‑byte shared secret (no 0x04 prefix) from secp256k1 ECDH
        private static string GetSharedSecretHex(string nsec, string npub)
        {
            var priv = new Key(Encoders.Hex.DecodeData(nsec));
            // npub is the 32‑byte raw X coordinate; prepend 0x02 for even‐Y compressed
            var pub = new PubKey("02" + npub);
            var secretPoint = pub.GetSharedPubkey(priv);
            // drop the 0x04 prefix byte from the 65‑byte output
            var raw = secretPoint.ToBytes();
            return Encoders.Hex.EncodeData(raw, 1, raw.Length - 1);
        }
    }
}

using Angor.Shared.Services;
using NBitcoin;
using NBitcoin.DataEncoders;
using Microsoft.JSInterop;
using Nostr.Client.Keys;
using Nostr.Client.Utils;

namespace Angor.Client.Services
{
    public class EncryptionService : IEncryptionService
    {
        private readonly IJSRuntime _jsRuntime;

        public EncryptionService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task<string> EncryptData(string secretData, string password)
        {
            return await _jsRuntime.InvokeAsync<string>("encryptData", secretData, password);
        }

        public async Task<string> DecryptData(string encryptedData, string password)
        {
            return await _jsRuntime.InvokeAsync<string>("decryptData", encryptedData, password);
        }

        /// <summary>
        /// Encrypts Nostr DM content with NIP-44 (the scheme used by the SDK/desktop app).
        /// </summary>
        public Task<string> EncryptNostrContentAsync(string nsec, string npub, string content)
        {
            var privateKey = NostrPrivateKey.FromHex(nsec);
            var publicKey = NostrPublicKey.FromHex(npub);
            var conversationKey = privateKey.DeriveConversationKeyNip44(publicKey);

            var encrypted = NostrEncryptionNip44.Encrypt(content, conversationKey);
            return Task.FromResult(encrypted);
        }

        /// <summary>
        /// Decrypts Nostr DM content. Auto-detects the scheme: NIP-04 payloads carry a
        /// "?iv=" separator; NIP-44 payloads are plain base64. Mirrors the SDK's
        /// EncryptionService so web and desktop can exchange messages either way.
        /// </summary>
        public async Task<string> DecryptNostrContentAsync(string nsec, string npub, string encryptedContent)
        {
            // Legacy NIP-04 (AES-CBC via the JS shim) for backward compatibility.
            if (encryptedContent.Contains("?iv="))
            {
                var secertHex = GetSharedSecretHexWithoutPrefix(nsec, npub);
                return await _jsRuntime.InvokeAsync<string>("decryptNostr", secertHex, encryptedContent);
            }

            var privateKey = NostrPrivateKey.FromHex(nsec);
            var publicKey = NostrPublicKey.FromHex(npub);
            var conversationKey = privateKey.DeriveConversationKeyNip44(publicKey);

            return NostrEncryptionNip44.Decrypt(encryptedContent, conversationKey);
        }

        private static string GetSharedSecretHexWithoutPrefix(string nsec, string npub)
        {
            var privateKey = new Key(Encoders.Hex.DecodeData(nsec));
            var publicKey = new PubKey("02" + npub);
            
            var secert = publicKey.GetSharedPubkey(privateKey);
            return Encoders.Hex.EncodeData(secert.ToBytes()[1..]);
        }
    }
}

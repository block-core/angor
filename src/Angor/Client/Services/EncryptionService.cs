using Microsoft.JSInterop;

namespace Angor.Client.Services
{
    /// <summary>
    /// Service responsible for encrypting and decrypting data, including Nostr messages.
    /// 
    /// Implements NIP-17 for Nostr encryption which replaces the deprecated NIP-04 standard.
    /// Key generation and cryptographic operations are performed directly in JavaScript 
    /// through the WebCrypto API for better security and standards compliance.
    /// </summary>
    public class EncryptionService : IEncryptionService
    {
        private readonly IJSRuntime _jsRuntime;

        public EncryptionService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        /// <summary>
        /// Encrypts data with a password
        /// </summary>
        public async Task<string> EncryptData(string secretData, string password)
        {
            return await _jsRuntime.InvokeAsync<string>("encryptData", secretData, password);
        }

        /// <summary>
        /// Decrypts data with a password
        /// </summary>
        public async Task<string> DecryptData(string encryptedData, string password)
        {
            return await _jsRuntime.InvokeAsync<string>("decryptData", encryptedData, password);
        }

        /// <summary>
        /// Encrypts Nostr content using NIP-17 standard
        /// 
        /// Passes the public key (npub) directly to JavaScript for encryption
        /// instead of calculating a shared secret in C# as in the old NIP-04 implementation
        /// </summary>
        public async Task<string> EncryptNostrContentAsync(string nsec, string npub, string content)
        {
            // Using NIP-17 for encryption
            return await _jsRuntime.InvokeAsync<string>("nip17Encrypt", npub, content);
        }

        /// <summary>
        /// Decrypts Nostr content using NIP-17 standard
        /// 
        /// Passes the private key (nsec) directly to JavaScript for decryption
        /// instead of calculating a shared secret in C# as in the old NIP-04 implementation
        /// </summary>
        public async Task<string> DecryptNostrContentAsync(string nsec, string npub, string encryptedContent)
        {
            // Using NIP-17 for decryption
            return await _jsRuntime.InvokeAsync<string>("nip17Decrypt", nsec, encryptedContent);
        }
    }
}

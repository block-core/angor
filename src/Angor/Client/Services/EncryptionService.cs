using Microsoft.JSInterop;

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

        public async Task<string> EncryptNostrContentAsync(string nsec, string npub, string content)
        {
            try
            {
                // First get the conversation key
                var conversationKey = await _jsRuntime.InvokeAsync<byte[]>("NostrTools.nip44.getConversationKey", nsec, npub);
                // Then encrypt the content - the result might be a Uint8Array in JavaScript
                var encryptedResult = await _jsRuntime.InvokeAsync<byte[]>("NostrTools.nip44.encrypt", conversationKey, content);
        
                // Convert the byte array to Base64 string if needed
                return Convert.ToBase64String(encryptedResult);
            }
            catch (JSException ex)
            {
                // Log or handle the exception appropriately
                throw new Exception($"Encryption failed: {ex.Message}", ex);
            }
        }

        public async Task<string> DecryptNostrContentAsync(string nsec, string npub, string encryptedContent)
        {
            try
            {
                var conversationKey = await _jsRuntime.InvokeAsync<byte[]>("NostrTools.nip44.getConversationKey", nsec, npub);
                // If the encryptedContent is base64, you might need to decode it first
                var encryptedBytes = Convert.FromBase64String(encryptedContent);
                return await _jsRuntime.InvokeAsync<string>("NostrTools.nip44.decrypt", conversationKey, encryptedBytes);
            }
            catch (JSException ex)
            {
                // Log or handle the exception appropriately
                throw new Exception($"Decryption failed: {ex.Message}", ex);
            }
        }

    }
}

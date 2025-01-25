using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;
using Blockcore.NBitcoin.DataEncoders;
using Microsoft.JSInterop;
using NBitcoin.Crypto;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Angor.Client.Services
{
    public class EncryptionService : IEncryptionService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly ILogger<EncryptionService> _logger;

        public EncryptionService(IJSRuntime jsRuntime, ILogger<EncryptionService> logger)
        {
            _jsRuntime = jsRuntime;
            _logger = logger;
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
            var secertHex = GetSharedSecretHexWithoutPrefix(nsec, npub);
            return await _jsRuntime.InvokeAsync<string>("encryptNostr", secertHex, content);
        }

        public async Task<string> DecryptNostrContentAsync(string nsec, string npub, string encryptedContent)
        {
            try
            {
                _logger.LogInformation($"Decrypting content with nsec: {nsec}, npub: {npub}, encryptedContent: {encryptedContent}");
                var decryptedContent = await _jsRuntime.InvokeAsync<string>("decryptNostrContent", nsec, npub, encryptedContent);
                _logger.LogInformation($"Decrypted content: {decryptedContent}");
                return decryptedContent;
            }
            catch (JSException jsEx)
            {
                _logger.LogError(jsEx, "JavaScript decryption failed");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Decryption failed");
                throw;
            }
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

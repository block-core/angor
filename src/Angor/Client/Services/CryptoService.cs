using Microsoft.JSInterop;

namespace Angor.Client.Services
{
    public class CryptoService : ICryptoService
    {
        private readonly IJSRuntime _jsRuntime;

        public CryptoService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task<string> EncryptDataAsync(string password, string secretData)
        {
            return await _jsRuntime.InvokeAsync<string>("encryptData", secretData, password);
        }

        public async Task<string> DecryptDataAsync(string password, string encryptedData)
        {
            return await _jsRuntime.InvokeAsync<string>("decryptData", encryptedData, password);
        }
    }

}

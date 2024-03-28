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
    }
}

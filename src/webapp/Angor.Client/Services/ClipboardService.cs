using Microsoft.JSInterop;

namespace Angor.Client.Services
{
    public class ClipboardService : IClipboardService
    {
        private readonly IJSRuntime _jsRuntime;

        public ClipboardService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task<string> ReadTextAsync()
        {
            return await _jsRuntime.InvokeAsync<string>("navigator.clipboard.readText");
        }

        public async Task WriteTextAsync(string text)
        {
            await _jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
        }
    }

}

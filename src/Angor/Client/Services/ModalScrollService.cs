using System.Threading.Tasks;
using Microsoft.JSInterop;

public class ModalScrollService
{
    private readonly IJSRuntime _jsRuntime;
    private int _openCount = 0;

    public ModalScrollService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task LockScrollAsync()
    {
        if (_openCount == 0)
        {
            await _jsRuntime.InvokeVoidAsync("ModalScrollManager.enableScrollLock");
        }
        _openCount++;
    }

    public async Task UnlockScrollAsync()
    {
        if (_openCount <= 1)
        {
            await _jsRuntime.InvokeVoidAsync("ModalScrollManager.disableScrollLock");
            _openCount = 0;
        }
        else
        {
            _openCount--;
        }
    }
}

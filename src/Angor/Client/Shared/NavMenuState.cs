using Microsoft.JSInterop;

namespace Angor.Client.Shared
{
    public class NavMenuState
    {
        private readonly IJSRuntime _jsRuntime;
        private const string StorageKey = "activeMenuPage";

        public NavMenuState(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public event Action OnChange;

        private string _activePage = string.Empty;
        public string ActivePage
        {
            get => _activePage;
            private set
            {
                if (_activePage != value)
                {
                    _activePage = value;
                    _ = PersistState();
                }
            }
        }

        public async Task SetActivePage(string page)
        {
            ActivePage = page;
            NotifyStateChanged();
            await PersistState();
        }

        public async Task InitializeFromStorage()
        {
            try
            {
                var storedPage = await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", StorageKey);
                if (!string.IsNullOrEmpty(storedPage))
                {
                    _activePage = storedPage;
                    NotifyStateChanged();
                }
            }
            catch 
            {
                // Ignore any errors during initialization
            }
        }

        private async Task PersistState()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", StorageKey, ActivePage);
            }
            catch 
            {
                // Ignore any errors during persistence
            }
        }

        public void NotifyStateChanged() => OnChange?.Invoke();
    }
}

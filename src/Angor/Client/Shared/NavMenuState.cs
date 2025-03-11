using Angor.Client.Services;
using Angor.Client.Storage;

namespace Angor.Client.Shared
{
    public class NavMenuState
    {
        private readonly ICacheStorage _cacheStorage;

        public NavMenuState(ICacheStorage cacheStorage)
        {
            _cacheStorage = cacheStorage;
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
                    PersistState();
                }
            }
        }

        public Task SetActivePage(string page)
        {
            ActivePage = page;
            NotifyStateChanged();
            PersistState();
            return Task.CompletedTask;
        }

        public Task InitializeFromStorage()
        {
            try
            {
                var storedPage = _cacheStorage.GetActiveMenuPage();
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

            return Task.CompletedTask;
        }

        private void PersistState()
        {
            try
            {
                _cacheStorage.SetActiveMenuPage(ActivePage);
            }
            catch 
            {
                // Ignore any errors during persistence
            }
        }

        public void NotifyStateChanged() => OnChange?.Invoke();
    }
}

namespace Angor.Client.Shared
{
    public class NavMenuState
    {
        private string _activePage = string.Empty;
        
        public string ActivePage => _activePage;

        public event Action OnChange;

        public void SetActivePage(string page)
        {
            if (_activePage != page)
            {
                _activePage = page;
                NotifyStateChanged();
            }
        }

        public void NotifyStateChanged() => OnChange?.Invoke();
    }
}

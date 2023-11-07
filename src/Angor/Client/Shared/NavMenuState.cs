using Angor.Client.Storage;
using Microsoft.AspNetCore.Components;

namespace Angor.Client.Shared
{
    public class NavMenuState 
    {
        public event Action OnChange;

        public void NotifyStateChanged() => OnChange?.Invoke();
    }
}

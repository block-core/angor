using Angor.Client.Storage;
using Microsoft.AspNetCore.Components;

namespace Angor.Client.Shared
{
    public class BaseComponent : ComponentBase
    {
        [Inject]
        protected IWalletStorage _walletStorage { get; set; }

        public NotificationComponent notificationComponent;

        protected bool hasWallet { get; set; }

        protected void SharedMethod()
        {
            // Shared logic here... 
        }

        protected override void OnInitialized()
        {
            hasWallet = _walletStorage.HasWallet();
        }
    }
}

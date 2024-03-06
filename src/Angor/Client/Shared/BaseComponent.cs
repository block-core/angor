using Angor.Client.Storage;
using Angor.Shared;
using Blockcore.Networks;
using Microsoft.AspNetCore.Components;

namespace Angor.Client.Shared
{
    public class BaseComponent : ComponentBase
    {
        [Inject]
        protected INetworkConfiguration NetworkConfiguration { get; set; }

        [Inject]
        protected IWalletStorage WalletStorage { get; set; }

        [Inject]
        protected NavigationManager NavigationManager { get; set; }

        public NotificationComponent NotificationComponent;
        protected bool HasWallet { get; set; }

        protected Network Network { get; set; }

        protected void SharedMethod()
        {
            // Shared logic here... 
        }

        protected override void OnInitialized()
        {
            HasWallet = WalletStorage.HasWallet();
            Network = NetworkConfiguration.GetNetwork();
        }
    }
}

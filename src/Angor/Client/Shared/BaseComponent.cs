using Angor.Client.Services;
using Angor.Client.Storage;
using Angor.Shared;
using Blockcore.Networks;
using Microsoft.AspNetCore.Components;

namespace Angor.Client.Shared
{
    public class BaseComponent : ComponentBase
    {
        [Inject]
        protected INetworkConfiguration _networkConfiguration { get; set; }

        [Inject]
        protected IWalletStorage _walletStorage { get; set; }

        [Inject]
        protected NavigationManager NavigationManager { get; set; }

        [Inject]
        protected IPasswordCacheService _PasswordCacheService { get; set; }

        public NotificationComponent notificationComponent;
        public PasswordComponent passwordComponent;

        protected bool hasWallet { get; set; }

        protected Network network { get; set; }

        protected void SharedMethod()
        {
            // Shared logic here... 
        }

        protected override void OnInitialized()
        {
            hasWallet = _walletStorage.HasWallet();
            network = _networkConfiguration.GetNetwork();

            _PasswordCacheService.ResetLastSet();
        }
    }
}

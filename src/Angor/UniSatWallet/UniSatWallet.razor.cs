using System;
using System.Threading.Tasks;
using UniSatWallet;
using UniSatWallet.Exceptions;
using Microsoft.AspNetCore.Components;


namespace UniSatWallet
{

    public partial class UniSatWallet : IDisposable
    {
        private bool disposedValue; 
        public bool HasUniSatWallet { get; set; }

        private string? accounts;
        private string? accountsResult;
        private string? networkResult;
        private string? switchNetworkResult;
        private string? publicKeyResult;
        private string? balanceResult;
        private string? inscriptionsResult;
        private string? sendBitcoinResult;
        private string? sendInscriptionResult;
        private string? signMessageResult;
        private string? pushTransactionResult;
        private string? signPsbtResult;
        private string? signPsbtsResult;
        private string? pushPsbtResult;


        [Inject]
        public IUniSatWalletConnector UniSatWalletConnector { get; set; } = default!;



        protected override async Task OnInitializedAsync()
        {
            HasUniSatWallet = await UniSatWalletConnector.HasUniSatWallet();
        }

        private async Task ConnectUniSatWallet()
        {
            accounts = await UniSatWalletConnector.ConnectUniSatWallet();
        }

        private async Task GetUniSatAccounts()
        {
            accountsResult = await UniSatWalletConnector.GetUniSatAccounts();
        }

        private async Task GetUniSatNetwork()
        {
            networkResult = await UniSatWalletConnector.GetUniSatNetwork();
        }

        private async Task SwitchUniSatNetwork()
        {
            switchNetworkResult = await UniSatWalletConnector.SwitchUniSatNetwork("livenet"); // or "testnet"
        }

        private async Task GetUniSatPublicKey()
        {
            publicKeyResult = await UniSatWalletConnector.GetUniSatPublicKey();
        }

        private async Task GetUniSatBalance()
        {
            balanceResult = await UniSatWalletConnector.GetUniSatBalance();
        }

        private async Task GetUniSatInscriptions()
        {
             inscriptionsResult = await UniSatWalletConnector.GetUniSatInscriptions(0, 10);
        }

        private async Task SendBitcoinUniSat()
        {
             sendBitcoinResult = await UniSatWalletConnector.SendBitcoinUniSat("toAddress", 1000, new { });
        }

        private async Task SendInscriptionUniSat()
        {
             sendInscriptionResult = await UniSatWalletConnector.SendInscriptionUniSat("address", "inscriptionId", new { });
        }

        private async Task SignMessageUniSat()
        {
             signMessageResult = await UniSatWalletConnector.SignMessageUniSat("message", "type");
        }

        private async Task PushTransactionUniSat()
        {
             pushTransactionResult = await UniSatWalletConnector.PushTransactionUniSat(new { });
        }

        private async Task SignPsbtUniSat()
        {
             signPsbtResult = await UniSatWalletConnector.SignPsbtUniSat("psbtHex", new { });
        }

        private async Task SignPsbtsUniSat()
        {
             signPsbtsResult = await UniSatWalletConnector.SignPsbtsUniSat(new[] { "psbtHex1", "psbtHex2" }, new { });
        }

        private async Task PushPsbtUniSat()
        {
             pushPsbtResult = await UniSatWalletConnector.PushPsbtUniSat("psbtHex");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }




        public void Dispose()
        {
             Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }


}




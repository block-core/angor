using System.Threading.Tasks;
using Angor.UI.Model.Implementation.Wallet;
using Angor.Wallet.Application;
using Angor.Wallet.Domain;

namespace AngorApp.UI.Services;

public class WalletBuilder(IWalletAppService walletAppService, ITransactionWatcher transactionWatcher) : IWalletBuilder
{
    public async Task<Result<IWallet>> Create(WalletId walletId)
    {
        return new DynamicWallet(walletId, walletAppService, transactionWatcher);
    }
}
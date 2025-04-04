using System.Threading.Tasks;
using Angor.UI.Model.Implementation.Wallet;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;

namespace AngorApp.UI.Services;

public class WalletBuilder(IWalletAppService walletAppService, ITransactionWatcher transactionWatcher) : IWalletBuilder
{
    public async Task<Result<IWallet>> Create(WalletId walletId)
    {
        return new DynamicWallet(walletId, walletAppService, transactionWatcher);
    }
}
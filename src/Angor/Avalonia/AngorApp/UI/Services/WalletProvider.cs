using System.Threading.Tasks;
using Angor.UI.Model.Implementation.Wallet;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using Zafiro.UI;

namespace AngorApp.UI.Services;

public class WalletProvider(IWalletAppService walletAppService, ITransactionWatcher transactionWatcher, INotificationService notificationService) : IWalletBuilder
{
    private readonly Dictionary<WalletId, DynamicWallet> runningWallets = new ();
    
    public async Task<Result<IWallet>> Get(WalletId walletId)
    {
        if (runningWallets.TryGetValue(walletId, out var wallet))
        {
            return wallet;
        }
        
        var dynamicWallet = new DynamicWallet(walletId, walletAppService, transactionWatcher);
        
        var tcs = new TaskCompletionSource<Result<IWallet>>();

        IDisposable syncSubscription = null;
        
        dynamicWallet.Sync.StartReactive.Take(1)
            .Subscribe(result =>
            {
                if (result.IsSuccess)
                {
                    runningWallets.Add(walletId, dynamicWallet);
                    tcs.SetResult(Result.Success<IWallet>(dynamicWallet));
                }
                else
                {
                    tcs.SetResult(Result.Failure<IWallet>(result.Error));
                    syncSubscription?.Dispose();
                }
            });

        syncSubscription = dynamicWallet.Sync.StartReactive.Execute().Subscribe();
        
        return await tcs.Task;
    }
}
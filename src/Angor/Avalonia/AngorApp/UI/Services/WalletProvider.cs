using System.Threading.Tasks;
using Angor.UI.Model.Implementation.Wallet;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using Zafiro.UI;

namespace AngorApp.UI.Services;

public class WalletProvider(IWalletAppService walletAppService, ITransactionWatcher transactionWatcher, UIServices uiServices) : IWalletBuilder
{
    private readonly Dictionary<WalletId, DynamicWallet> runningWallets = new ();
    
    public async Task<Result<IWallet>> Get(WalletId walletId)
    {
        if (runningWallets.ContainsKey(walletId))
        {
            throw new InvalidOperationException($"A DynamicWallet with WalletId: {walletId} is already running. We should run more than one wallet for the same WalletId.");
        }
        
        var dynamicWallet = new DynamicWallet(walletId, walletAppService, transactionWatcher);
        
        var tcs = new TaskCompletionSource<Result<IWallet>>();

        IDisposable syncSubscription = null!;
        
        dynamicWallet.SyncCommand.StartReactive.Take(1)
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
                    syncSubscription.Dispose();
                }
            });

        dynamicWallet.SyncCommand.StartReactive.HandleErrorsWith(uiServices.NotificationService, "Wallet Sync Error");
        syncSubscription = dynamicWallet.SyncCommand.StartReactive.Execute().Subscribe();
        
        return await tcs.Task;
    }
}
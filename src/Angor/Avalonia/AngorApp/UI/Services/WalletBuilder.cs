using System.Threading.Tasks;
using Angor.UI.Model.Implementation.Wallet;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;

namespace AngorApp.UI.Services;

public class WalletBuilder(IWalletAppService walletAppService, ITransactionWatcher transactionWatcher) : IWalletBuilder
{
    private readonly Dictionary<WalletId, DynamicWallet> createdWallets = new ();
    
    public async Task<Result<IWallet>> Create(WalletId walletId)
    {
        if (createdWallets.ContainsKey(walletId))
        {
            throw new InvalidOperationException($"A DynamicWallet was created for the same WalletId: {walletId}. We should not create more than one instance.");
        }
        
        var dynamicWallet = new DynamicWallet(walletId, walletAppService, transactionWatcher);
        
        createdWallets.Add(walletId, dynamicWallet);
        
        var tcs = new TaskCompletionSource<Result<IWallet>>();

        IDisposable syncSubscription = null!;
        
        dynamicWallet.SyncCommand.StartReactive.Take(1)
            .Subscribe(result =>
            {
                if (result.IsSuccess)
                {
                    tcs.SetResult(Result.Success<IWallet>(dynamicWallet));
                }
                else
                {
                    tcs.SetResult(Result.Failure<IWallet>(result.Error));
                    syncSubscription.Dispose();
                }
            });
        
        dynamicWallet.SyncCommand.StartReactive.Execute().Subscribe();
        
        return await tcs.Task;
    }
}
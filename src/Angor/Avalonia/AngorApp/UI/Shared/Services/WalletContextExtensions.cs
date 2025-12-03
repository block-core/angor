namespace AngorApp.UI.Shared.Services;

public static class WalletContextExtensions
{
    public static Task<Result<T>> RequiresWallet<T>(this IWalletContext walletContext, Func<IWallet, Task<Result<T>>> resultFactory)
    {
        return walletContext
            .GetDefaultWallet()
            .Bind(resultFactory);
    }
    
    public static async Task<Result> RequiresWallet(this IWalletContext walletContext, Func<IWallet, Task<Result>> resultFactory)
    {
        return await walletContext
            .GetDefaultWallet()
            .Bind(resultFactory);
    }
    
    public static Task<Result<T>> RequiresWallet<T>(this IWalletContext walletContext, Func<IWallet, Task<T>> func)
    {
        return walletContext
            .GetDefaultWallet()
            .Map(func);
    }
}
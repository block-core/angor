namespace AngorApp.UI.Shared.Services;

public static class WalletContextExtensions
{
    public static Task<Result<T>> RequiresWallet<T>(this IWalletContext walletContext, Func<IWallet, Task<Result<T>>> func)
    {
        return walletContext
            .GetDefaultWallet()
            .Bind(func);
    }
    
    public static Task<Result> RequiresWallet(this IWalletContext walletContext, Func<IWallet, Task<Result>> func)
    {
        return walletContext
            .GetDefaultWallet()
            .Bind(func);
    }
    
    public static Task<Result<T>> RequiresWallet<T>(this IWalletContext walletContext, Func<IWallet, Task<T>> func)
    {
        return walletContext.GetDefaultWallet()
            .Map(func);
    }
}
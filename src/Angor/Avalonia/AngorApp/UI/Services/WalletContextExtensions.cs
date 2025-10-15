namespace AngorApp.UI.Services;

public static class WalletContextExtensions
{
    public static Result<T> WithActiveWallet<T>(this IWalletContext walletContext, Func<IWallet, T> func)
    {
        return walletContext.CurrentWallet
            .ToResult("Please, create a wallet first")
            .Map(func);
    }

    public static Task<Result<T>> RequiresWallet<T>(this IWalletContext walletContext, Func<IWallet, Task<Result<T>>> func)
    {
        return walletContext.CurrentWallet
            .ToResult("Please, create a wallet first")
            .Map(func);
    }
    
    public static async Task<Result> RequiresWallet(this IWalletContext walletContext, Func<IWallet, Task<Result>> func)
    {
        return await walletContext.CurrentWallet
            .ToResult("Please, create a wallet first")
            .Map(func);
    }
    
    public static Task<Result<T>> RequiresWallet<T>(this IWalletContext walletContext, Func<IWallet, Task<T>> func)
    {
        return walletContext.CurrentWallet
            .ToResult("Please, create a wallet first")
            .Map(func);
    }
}
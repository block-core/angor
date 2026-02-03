namespace AngorApp.UI.Shared.Services;

public static class WalletContextExtensions
{
    public static Task<Result<IWallet>> Require(this IWalletContext context) 
        => context.TryGet().ToResult("Wallet required");
}

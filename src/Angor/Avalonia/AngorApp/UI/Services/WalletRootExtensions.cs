using System.Threading.Tasks;

namespace AngorApp.UI.Services;

public static class WalletRootExtensions
{
    public static Task<Result<IWallet>> TryDefaultWalletAndActivate(this IWalletRoot walletRoot, string reason = "Please, create a wallet first")
    {
        return walletRoot.GetDefaultWalletAndActivate().Bind(maybe => maybe.ToResult("Please, create a wallet first"));
    }
}
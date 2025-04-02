using System.Threading.Tasks;
using Angor.Contexts.Wallet.Domain;
using AngorApp.Sections.Wallet;

namespace AngorApp.Design;

public class WalletBuilderDesign : IWalletBuilder
{
    public async Task<Result<IWallet>> Create(WalletId walletId)
    {
        await Task.Delay(2000);
        return new WalletDesign();
    }
}
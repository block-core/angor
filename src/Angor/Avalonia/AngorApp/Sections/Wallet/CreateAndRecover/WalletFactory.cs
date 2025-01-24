using System.Threading.Tasks;
using AngorApp.Model;
using AngorApp.Services;
using CSharpFunctionalExtensions;

namespace AngorApp.Sections.Wallet.CreateAndRecover;

public class WalletFactory : IWalletFactory
{
    private readonly UIServices uiServices;
    private readonly IWalletBuilder walletBuilder;

    public WalletFactory(IWalletBuilder walletBuilder, UIServices uiServices)
    {
        this.walletBuilder = walletBuilder;
        this.uiServices = uiServices;
    }

    public Task<Maybe<Result<IWallet>>> Recover()
    {
        return new Recover(uiServices, walletBuilder).Start();
    }

    public Task<Maybe<Result<IWallet>>> Create()
    {
        return new Create(uiServices, walletBuilder).Start();
    }
}
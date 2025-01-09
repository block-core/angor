using System.Threading.Tasks;
using AngorApp.Model;
using AngorApp.Sections.Wallet.NoWallet;
using AngorApp.Sections.Wallet.Operate;
using AngorApp.Services;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.Sections.Wallet;

public class WalletFactoryDesign : IWalletFactory
{
    private readonly UIServices uiServices;

    public WalletFactoryDesign(UIServices uiServices)
    {
        this.uiServices = uiServices;
    }
    
    public Task<Maybe<Result<IWallet>>> Recover()
    {
        throw new NotImplementedException();
    }

    public async Task<Maybe<Result<IWallet>>> Create()
    {
        await uiServices.Dialog.ShowMessage("Info", "We will create a test wallet");
        return Result.Success<IWallet>(new WalletDesign());
    }
}
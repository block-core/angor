using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Application;
using Angor.Shared;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Dialogs;
using Zafiro.UI;

namespace AngorApp.Model.Wallet.Simple;

public class SimpleWalletProvider(IWalletAppService walletAppService, ISendMoneyFlow sendMoneyFlow, INotificationService notificationService, INetworkConfiguration networkConfiguration, IDialog dialogService) : IWalletProvider
{
    public async Task<Result<IWallet>> Get(WalletId walletId)
    {
        return Result.Success<IWallet>(new SimpleWallet(walletId, walletAppService, sendMoneyFlow, dialogService, notificationService, networkConfiguration));
    }
}

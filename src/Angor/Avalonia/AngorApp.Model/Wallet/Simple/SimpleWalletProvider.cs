using Angor.Contexts.CrossCutting;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using Angor.Shared;
using AngorApp.Model.Contracts.Flows;
using AngorApp.Model.Contracts.Wallet;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Dialogs;
using Zafiro.UI;

namespace AngorApp.Model.Wallet;

public class SimpleWalletProvider(IWalletAppService walletAppService, ISendMoneyFlow sendMoneyFlow, INotificationService notificationService, INetworkConfiguration networkConfiguration, IDialog dialogService) : IWalletProvider
{
    public async Task<Result<IWallet>> Get(WalletId walletId)
    {
        return Result.Success<IWallet>(new SimpleWallet(walletId, walletAppService, sendMoneyFlow, dialogService, notificationService, networkConfiguration));
    }
}

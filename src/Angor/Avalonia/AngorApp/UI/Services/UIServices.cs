using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Services;
using Zafiro.UI;

namespace AngorApp.UI.Services;

public class UIServices
{
    public ILauncherService LauncherService { get; }
    public IDialog Dialog { get; }
    public INotificationService NotificationService { get; }
    public IActiveWallet ActiveWallet { get; }

    public IWalletRoot WalletRoot { get; }

    public UIServices(ILauncherService launcherService, IDialog dialog, INotificationService notificationService,
        IActiveWallet activeWallet,
        IWalletRoot walletRoot)
    {
        LauncherService = launcherService;
        Dialog = dialog;
        NotificationService = notificationService;
        ActiveWallet = activeWallet;
        WalletRoot = walletRoot;
    }
}
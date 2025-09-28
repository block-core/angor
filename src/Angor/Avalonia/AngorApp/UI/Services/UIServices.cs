using AngorApp.UI.Controls;
using AngorApp.UI.Controls.Feerate;
using Avalonia;
using Avalonia.Styling;
using ReactiveUI.SourceGenerators;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Services;
using Zafiro.UI;
using Preset = AngorApp.UI.Controls.Feerate.Preset;

namespace AngorApp.UI.Services;

public partial class UIServices : ReactiveObject
{
    [Reactive] private bool isDarkThemeEnabled;
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
        this.WhenAnyValue(services => services.IsDarkThemeEnabled)
            .Do(isDarkTheme => Application.Current.RequestedThemeVariant = isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light)
            .Subscribe();
    }

    public IEnumerable<IFeeratePreset> FeeratePresets
    {
        get
        {
            return new[]
            {
                new Preset("Economy", new AmountUI(2), null, null),
                new Preset("Standard", new AmountUI(12), null, null),
                new Preset("Priority", new AmountUI(20), null, null),
            };
        }
    }
}
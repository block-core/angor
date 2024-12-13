using Zafiro.Avalonia.Dialogs.Simple;
using Zafiro.UI;

namespace AngorApp.Services;

public class UIServices
{
    public ILauncherService LauncherService { get; }
    public IDialog Dialog { get; }
    public INotificationService NotificationService { get; }

    public UIServices(ILauncherService launcherService, IDialog dialog, INotificationService notificationService)
    {
        LauncherService = launcherService;
        Dialog = dialog;
        NotificationService = notificationService;
    }
}
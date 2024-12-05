namespace AngorApp.Services;

public class UIServices
{
    public ILauncherService LauncherService { get; }

    public UIServices(ILauncherService launcherService)
    {
        LauncherService = launcherService;
    }
}
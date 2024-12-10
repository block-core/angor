using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace AngorApp.Services;

public class LauncherService : ILauncherService
{
    private readonly ILauncher launcher;

    public LauncherService(ILauncher launcher)
    {
        this.launcher = launcher;
    }

    public Task Launch(Uri uri) => launcher.LaunchUriAsync(uri);
}
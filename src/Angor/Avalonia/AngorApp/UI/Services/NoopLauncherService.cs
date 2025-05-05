using System.Threading.Tasks;
using Zafiro.Avalonia.Services;

namespace AngorApp.UI.Services;

public class NoopLauncherService : ILauncherService
{
    public Task LaunchUri(Uri uri)
    {
        return Task.CompletedTask;
    }
}
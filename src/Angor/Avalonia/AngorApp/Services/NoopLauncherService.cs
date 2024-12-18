using System.Threading.Tasks;
using Zafiro.Avalonia.Services;

namespace AngorApp.Services;

public class NoopLauncherService : ILauncherService
{
    public Task LaunchUri(Uri uri)
    {
        return Task.CompletedTask;
    }
}
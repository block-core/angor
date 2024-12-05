using System.Threading.Tasks;

namespace AngorApp.Services;

public class NoopLauncherService : ILauncherService
{
    public Task Launch(Uri uri)
    {
        return Task.CompletedTask;
    }
}
using System.Threading.Tasks;

namespace AngorApp.Services;

public interface ILauncherService
{
    Task Launch(Uri uri);
}
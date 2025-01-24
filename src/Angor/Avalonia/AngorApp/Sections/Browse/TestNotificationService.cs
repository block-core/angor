using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Zafiro.UI;

namespace AngorApp.Sections.Browse;

public class TestNotificationService : INotificationService
{
    public Task Show(string message, Maybe<string> title)
    {
        return Task.CompletedTask;
    }
}
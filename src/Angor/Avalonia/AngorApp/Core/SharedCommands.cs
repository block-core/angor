using Angor.Shared;
using Zafiro.Avalonia.Services;

namespace AngorApp.Core;

public class SharedCommands(INetworkStorage networkStorage, ILauncherService launcherService)
{
    public IEnhancedCommand OpenTransaction(string transactionId)
    {
        return ReactiveCommand.CreateFromTask(async () =>
        {
            var settings = networkStorage.GetSettings();
            return settings.Explorers.TryFirst(setting => setting.IsPrimary)
                .ToResult("No primary explorer found")
                .Map(explorer => new Uri(new Uri(explorer.Url, UriKind.Absolute), $"tx/{transactionId}"))
                .Bind(url => launcherService.LaunchUri(url));
            
        }).Enhance();
    }
}
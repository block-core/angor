using Angor.Shared;
using Zafiro.Avalonia;

namespace AngorApp.Core;

public class SharedCommands
{
    private readonly UIServices uiServices;
    private readonly INetworkStorage networkStorage;

    public SharedCommands(UIServices uiServices, INetworkStorage networkStorage)
    {
        this.uiServices = uiServices;
        this.networkStorage = networkStorage;
    }
    
    public IEnhancedCommand OpenTransaction(string transactionId)
    {
        // return ReactiveCommand.CreateFromTask(async () =>
        // {
        //     var settings = networkStorage.GetSettings();
        //     Result tapTry = await settings.Explorers.TryFirst(setting => setting.IsPrimary)
        //         .ToResult("No primary explorer found")
        //         .Map(explorer => new Uri(new Uri(explorer.Url, UriKind.Absolute), $"tx/{transactionId}"))
        //         .TapTry(async url =>
        //         {
        //             await Commands.Instance.LaunchUri(url);
        //         }, exception => "Failed to open transaction in explorer: " + exception.Message);
        //     
        //     return tapTry;
        // }).Enhance();
        return ReactiveCommand.Create(() => { }).Enhance();
    }
}
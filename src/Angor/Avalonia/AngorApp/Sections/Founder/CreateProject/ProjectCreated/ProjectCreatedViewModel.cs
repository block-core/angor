using Angor.Shared;
using AngorApp.UI.Services;
using Zafiro.UI;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.CreateProject.ProjectCreated;

internal class ProjectCreatedViewModel : IProjectCreatedViewModel
{
    public ProjectCreatedViewModel(string transactionId, UIServices uiServices, INetworkStorage networkConfiguration)
    {
        var settings = networkConfiguration.GetSettings();
        OpenTransaction = ReactiveCommand.CreateFromTask(async () =>
        {
            Result tapTry = await settings.Explorers.TryFirst(setting => setting.IsPrimary)
                .ToResult("No primary explorer found")
                .Map(explorer => new Uri(new Uri(explorer.Url, UriKind.Absolute), $"tx/{transactionId}"))
                .TapTry(async url =>
                {
                    await uiServices.LauncherService.LaunchUri(url);
                });
            
            return tapTry;
        }).Enhance();

        OpenTransaction.HandleErrorsWith(uiServices.NotificationService, "Failed to open transaction in explorer");
    }

    public IEnhancedCommand<Result> OpenTransaction { get; }
}
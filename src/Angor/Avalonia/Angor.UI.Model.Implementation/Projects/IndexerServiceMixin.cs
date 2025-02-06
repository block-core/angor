using System.Reactive.Linq;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Zafiro.Reactive;

namespace Angor.UI.Model.Implementation.Projects;

public static class IndexerServiceMixin
{
    public static IObservable<ProjectIndexerData> GetLatest(
        this IIndexerService projectService,
        int pageSize = 20)
    {
        return Observable.FromAsync(() => projectService.GetProjectsAsync(null, pageSize)).Flatten();
    }
}
using System.Reactive.Linq;
using Angor.Shared.Services;
using Zafiro.Mixins;
using Zafiro.Reactive;

namespace Angor.Model.Implementation.Projects;

public static class IndexerServiceMixin
{
    public static IObservable<ProjectIndexerData> GetLatest(
        this IIndexerService projectService,
        int pageSize = 20)
    {
        return Observable.FromAsync(() => projectService.GetProjectsAsync(null, pageSize)).Flatten();
    }
}
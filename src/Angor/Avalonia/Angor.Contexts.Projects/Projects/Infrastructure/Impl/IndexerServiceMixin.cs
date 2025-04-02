using System.Reactive.Linq;
using Angor.Shared.Services;
using Zafiro.Reactive;

namespace Angor.Contexts.Projects.Infrastructure.Impl;

public static class IndexerServiceMixin
{
    public static IObservable<ProjectIndexerData> GetLatest(
        this IIndexerService projectService,
        int pageSize = 20)
    {
        return Observable.FromAsync(() => projectService.GetProjectsAsync(null, pageSize)).Flatten();
    }
}
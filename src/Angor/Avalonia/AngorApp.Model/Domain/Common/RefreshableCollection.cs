using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using DynamicData;
using System.Reactive.Linq;
using ReactiveUI;
using CSharpFunctionalExtensions;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Commands;

namespace AngorApp.Model.Domain.Common;

public static class RefreshableCollection
{
    public static RefreshableCollection<TItem, TKey> Create<TItem, TKey>(Func<Task<Result<IEnumerable<TItem>>>> getItems, Func<TItem, TKey> getKey)
        where TItem : notnull
        where TKey : notnull
    {
        return new RefreshableCollection<TItem, TKey>(getItems, getKey);
    }
}

public class RefreshableCollection<TItem, TKey> : IDisposable
    where TItem : notnull
    where TKey : notnull
{
    private readonly CompositeDisposable disposable = new();

    public RefreshableCollection(Func<Task<Result<IEnumerable<TItem>>>> getItems, Func<TItem, TKey> getKey)
    {
        Refresh = ReactiveCommand.CreateFromTask(getItems).Enhance().DisposeWith(disposable);

        var updates = Refresh.Successes()
            .EditDiff(getKey)
            .Publish();

        updates
            .DisposeMany()
            .Bind(out var items)
            .Subscribe()
            .DisposeWith(disposable);

        updates.Connect().DisposeWith(disposable);

        Changes = updates;
        Items = items;
    }

    public ReadOnlyObservableCollection<TItem> Items { get; }
    public IObservable<IChangeSet<TItem, TKey>> Changes { get; }
    public IEnhancedCommand<Result<IEnumerable<TItem>>> Refresh { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }
}

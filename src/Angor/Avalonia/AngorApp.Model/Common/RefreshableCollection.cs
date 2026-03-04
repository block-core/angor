using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using DynamicData;
using System.Reactive.Linq;
using ReactiveUI;
using CSharpFunctionalExtensions;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Commands;

namespace AngorApp.Model.Common;

public static class RefreshableCollection
{
    public static RefreshableCollection<TItem, TKey> Create<TItem, TKey>(
        Func<Task<Result<IEnumerable<TItem>>>> getItems,
        Func<TItem, TKey> getKey,
        Func<TItem, IComparable>? sortBy = null)
        where TItem : notnull
        where TKey : notnull
    {
        return new RefreshableCollection<TItem, TKey>(getItems, getKey, sortBy);
    }
}

public class RefreshableCollection<TItem, TKey> : IDisposable
    where TItem : notnull
    where TKey : notnull
{
    private readonly CompositeDisposable disposable = new();

    public RefreshableCollection(
        Func<Task<Result<IEnumerable<TItem>>>> getItems,
        Func<TItem, TKey> getKey,
        Func<TItem, IComparable>? sortBy = null)
    {
        Refresh = ReactiveCommand.CreateFromTask(getItems).Enhance().DisposeWith(disposable);

        var updates = Refresh.Successes()
            .EditDiff(getKey)
            .Publish();

        if (sortBy is null)
        {
            updates
                .DisposeMany()
                .Bind(out var items)
                .Subscribe()
                .DisposeWith(disposable);

            Items = items;
        }
        else
        {
            updates
                .DisposeMany()
                .SortBy(sortBy)
                .Bind(out var items)
                .Subscribe()
                .DisposeWith(disposable);

            Items = items;
        }

        updates.Connect().DisposeWith(disposable);

        Changes = updates;
    }

    public ReadOnlyObservableCollection<TItem> Items { get; }
    public IObservable<IChangeSet<TItem, TKey>> Changes { get; }
    public IEnhancedCommand<Result<IEnumerable<TItem>>> Refresh { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }
}

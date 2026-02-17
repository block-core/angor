using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using DynamicData;

namespace AngorApp.UI.Sections.Funders.Grouping;

using AngorApp.UI.Sections.Funders.Items;
public class FunderGroup : IFunderGroup, IDisposable
{
    private readonly CompositeDisposable disposable = new();

    public FunderGroup(string name, IObservable<IChangeSet<IFunderItem, string>> filter)
    {
        Name = name;
        filter.Bind(out ReadOnlyObservableCollection<IFunderItem> funders)
              .Subscribe()
              .DisposeWith(disposable);

        Funders = funders;
    }

    public void Dispose()
    {
        disposable.Dispose();
    }

    public string Name { get; }
    public IReadOnlyCollection<IFunderItem> Funders { get; }
}
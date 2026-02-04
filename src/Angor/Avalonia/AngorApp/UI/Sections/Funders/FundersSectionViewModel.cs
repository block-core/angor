using System.Reactive.Disposables;
using Zafiro.UI.Shell.Utils;

namespace AngorApp.UI.Sections.Funders;

[Section("Funders", icon: "fa-user-group", sortIndex: 5)]
[SectionGroup("FOUNDER")]
public class FundersSectionViewModel : ReactiveObject, IFundersSectionViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = new();

    public FundersSectionViewModel(UIServices uiServices)
    {
        ApproveAll = ReactiveCommand.Create(() => { }).Enhance().DisposeWith(disposable);
    }

    public IEnhancedCommand ApproveAll { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }
}

using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Browse.Details.Invest.Commit;

public class CommitViewModel : ReactiveObject, IStep
{
    public IObservable<bool> IsValid { get; }
    public IObservable<bool> IsBusy { get; }
    public bool AutoAdvance { get; }
}
using System.Reactive.Linq;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Common;

public class SuccessViewModel : IStep
{
    public string Message { get; }

    public SuccessViewModel(string message)
    {
        Message = message;
    }

    public IObservable<bool> IsValid => Observable.Return(false);
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;
}
using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Common.Success;

public class SuccessViewModel(string message, Maybe<string> title) : IStep
{
    public string Message { get; } = message;

    public IObservable<bool> IsValid => Observable.Return(false);
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;

    public Maybe<string> Title { get; } = title;
}
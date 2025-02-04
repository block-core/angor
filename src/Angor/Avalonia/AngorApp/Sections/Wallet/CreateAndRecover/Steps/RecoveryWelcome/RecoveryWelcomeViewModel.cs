using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.RecoveryWelcome;

public class RecoveryWelcomeViewModel : ReactiveValidationObject, IStep
{
    public IObservable<bool> IsValid => this.IsValid();
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;
    public Maybe<string> Title => "Wallet Recovery";
}
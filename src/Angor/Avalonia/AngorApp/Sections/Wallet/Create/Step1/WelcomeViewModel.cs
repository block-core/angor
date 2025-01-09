using System.Reactive.Linq;
using CSharpFunctionalExtensions;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Wallet.Create.Step1;

public partial class WelcomeViewModel : ReactiveValidationObject, IStep
{
    [Reactive] private bool isUserAware;
    
    public WelcomeViewModel()
    {
        this.ValidationRule<WelcomeViewModel, bool>(x => x.IsUserAware, x => x == true, "You cannot continue unless you understand the risks");
    }
    
    public IObservable<bool> IsValid => this.IsValid();
    public IObservable<bool> IsBusy => Observable.Return(false);
    public bool AutoAdvance => false;
    public Maybe<string> Title => "Welcome to the Wallet Creation Wizard!";
}
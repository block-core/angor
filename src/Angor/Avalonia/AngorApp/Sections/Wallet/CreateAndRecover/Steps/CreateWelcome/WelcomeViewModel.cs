using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.Sections.Wallet.CreateAndRecover.Steps.CreateWelcome;

public partial class WelcomeViewModel : ReactiveValidationObject
{
    [Reactive] private bool isUserAware;
    
    public WelcomeViewModel()
    {
        this.ValidationRule(x => x.IsUserAware, x => x, "You cannot continue unless you understand the risks");
    }
    
    public IObservable<bool> IsValid => this.IsValid();
}
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace AngorApp.UI.Sections.Wallet.CreateAndImport.Steps.CreateWelcome;

public partial class WelcomeViewModel : ReactiveValidationObject, IValidatable
{
    [Reactive] private bool isUserAware;
    
    public WelcomeViewModel()
    {
        this.ValidationRule<WelcomeViewModel, bool>(x => x.IsUserAware, x => x, "You cannot continue unless you understand the risks");
    }
    
    public IObservable<bool> IsValid => this.IsValid();
}

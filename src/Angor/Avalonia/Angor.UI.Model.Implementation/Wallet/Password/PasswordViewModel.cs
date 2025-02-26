using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace Angor.UI.Model.Implementation.Wallet.Password;

public partial class PasswordViewModel : ReactiveValidationObject
{
    public PasswordViewModel(string text)
    {
        Text = text;
        this.ValidationRule<PasswordViewModel, string>(x => x.Password, x => !string.IsNullOrWhiteSpace(x), "Can't be empty");
    }

    [Reactive] private string? password;
    public string Text { get; }
}
using CSharpFunctionalExtensions;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;

namespace Angor.UI.Model.Implementation.Wallet.Password;

public partial class PasswordViewModel : ReactiveValidationObject, IPasswordViewModel
{
    public PasswordViewModel(string text, object? icon = null)
    {
        Text = text;
        Icon = icon;
        this.ValidationRule<PasswordViewModel, string>(x => x.Password, x => !string.IsNullOrWhiteSpace(x), "Can't be empty");
    }

    [Reactive] private string? password;
    public string Text { get; }

    public object? Icon { get; }
}
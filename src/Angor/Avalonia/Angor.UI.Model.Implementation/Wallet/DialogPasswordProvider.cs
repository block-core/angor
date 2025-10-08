using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using Angor.UI.Model.Implementation.Wallet.Password;
using CSharpFunctionalExtensions;
using ReactiveUI.Validation.Extensions;
using Zafiro.Avalonia.Dialogs;

namespace Angor.UI.Model.Implementation.Wallet;

public class DialogPasswordProvider(IDialog dialog, string text, string title, object? icon = null) : IPasswordProvider
{
    public Task<Maybe<string>> Get(WalletId walletId)
    {
        return dialog.ShowAndGetResult<PasswordViewModel, string>(new PasswordViewModel(text, icon), title, x => x.IsValid(), x => x.Password!);
    }
}
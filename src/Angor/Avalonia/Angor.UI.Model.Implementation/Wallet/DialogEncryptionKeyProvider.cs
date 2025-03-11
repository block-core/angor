using Angor.Wallet.Domain;
using Angor.Wallet.Infrastructure.Interfaces;
using Avalonia.Threading;
using CSharpFunctionalExtensions;
using ReactiveUI.Validation.Extensions;
using Zafiro.Avalonia.Dialogs;
using PasswordViewModel = Angor.UI.Model.Implementation.Wallet.Password.PasswordViewModel;

namespace Angor.UI.Model.Implementation.Wallet;

public class DialogEncryptionKeyProvider(IDialog dialog, string text) : IEncryptionKeyProvider
{
    public Task<Maybe<string>> Get(WalletId walletId)
    {
        return Dispatcher.UIThread.InvokeAsync(() =>
        {
            return dialog.ShowAndGetResult<PasswordViewModel, string>(new PasswordViewModel(text), "Encryption key", x => x.IsValid<PasswordViewModel>(), x => x.Password!);
        });
    }
}
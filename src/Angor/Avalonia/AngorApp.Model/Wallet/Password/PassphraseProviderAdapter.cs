using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Dialogs;
using Zafiro.UI;

namespace AngorApp.Model.Wallet.Password;

public class  PassphraseProviderAdapter(IDialog dialog): IPassphraseProvider
{
    public Task<Maybe<string>> Get(WalletId walletId)
    {
        return new DialogPasswordProvider(dialog, "Please, enter the passphrase", "Wallet Unlock", new Icon("mdi-lock")).Get(walletId);
    }
}
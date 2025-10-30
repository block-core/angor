using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Dialogs;
using Zafiro.UI;

namespace AngorApp.Model.Domain.Wallet.Password;

public class PasswordProviderAdapter(IDialog dialog): IPasswordProvider
{
    public Task<Maybe<string>> Get(WalletId walletId)
    {
        return new DialogPasswordProvider(dialog, "Please, enter your wallet's encryption key to unlock it", "Wallet Unlock", new Icon("mdi-lock")).Get(walletId);
    }
}
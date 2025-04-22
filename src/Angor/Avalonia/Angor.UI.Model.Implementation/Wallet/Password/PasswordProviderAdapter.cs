using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Dialogs;

namespace Angor.UI.Model.Implementation.Wallet.Password;

public class PasswordProviderAdapter(IDialog dialog): IPasswordProvider
{
    public Task<Maybe<string>> Get(WalletId walletId)
    {
        return new DialogPasswordProvider(dialog, "Please, enter your wallet's encryption key to unlock it").Get(walletId);
    }
}
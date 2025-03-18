using Angor.Wallet.Domain;
using Angor.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Dialogs;

namespace Angor.UI.Model.Implementation.Wallet.Password;

public class EncryptionKeyProviderAdapter(IDialog dialog): IEncryptionKeyProvider
{
    public Task<Maybe<string>> Get(WalletId walletId)
    {
        return new DialogEncryptionKeyProvider(dialog, "Please, enter the encryption key").Get(walletId);
    }
}
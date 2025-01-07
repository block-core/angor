using System.Threading.Tasks;
using AngorApp.Model;
using AngorApp.Sections.Wallet.Operate;
using CSharpFunctionalExtensions;

namespace AngorApp.Sections.Wallet.Create;

public class WalletBuilderDesign : IWalletBuilder
{
    public async Task<Result<IWallet>> Create(WordList seedwords, Maybe<string> passphrase, string encryptionKey)
    {
        await Task.Delay(2000);
        return new WalletDesign();
    }
}
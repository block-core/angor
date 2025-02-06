using System.Threading.Tasks;
using Angor.UI.Model;
using AngorApp.Sections.Wallet;
using CSharpFunctionalExtensions;

namespace AngorApp.Design;

public class WalletBuilderDesign : IWalletBuilder
{
    public async Task<Result<IWallet>> Create(SeedWords seedwords, Maybe<string> passphrase, string encryptionKey)
    {
        await Task.Delay(2000);
        return new WalletDesign();
    }
}
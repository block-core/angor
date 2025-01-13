using System.Threading.Tasks;
using AngorApp.Model;
using AngorApp.Sections.Wallet.Operate;
using CSharpFunctionalExtensions;

namespace AngorApp.Sections.Wallet;

public class WalletBuilderDesign : IWalletBuilder
{
    public async Task<Result<IWallet>> Create(SeedWords seedwords, Maybe<string> passphrase, string encryptionKey)
    {
        await Task.Delay(2000);
        return new WalletDesign();
    }
}
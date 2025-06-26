using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Impl;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Tests;

public class TestSensitiveWalletDataProvider : ISensitiveWalletDataProvider
{
    private readonly string seed;
    private readonly string passphrase;

    public TestSensitiveWalletDataProvider(string seed, string passphrase)
    {
        this.seed = seed;
        this.passphrase = passphrase;
    }

    public async Task<Result<(string seed, Maybe<string> passphrase)>> RequestSensitiveData(WalletId walletId)
    {
        if (walletId == WalletAppService.SingleWalletId)
        {
            return (seed, passphrase);
        }

        return Result.Failure<(string seed, Maybe<string> passphrase)>("Invalid id");
    }

    public void SetSensitiveData(WalletId id, (string seed, Maybe<string> passphrase) data)
    {
        throw new NotImplementedException();
    }
}
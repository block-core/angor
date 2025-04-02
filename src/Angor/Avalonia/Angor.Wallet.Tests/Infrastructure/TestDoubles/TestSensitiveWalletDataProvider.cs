using Angor.Wallet.Domain;
using Angor.Wallet.Infrastructure.Impl;
using Angor.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Wallet.Tests.Infrastructure.TestDoubles;

public class TestSensitiveWalletDataProvider : ISensitiveWalletDataProvider
{
    private readonly string _seed;
    private readonly string _passphrase;

    public TestSensitiveWalletDataProvider(string seed, string passphrase)
    {
        _seed = seed;
        _passphrase = passphrase;
    }

    public async Task<Result<(string seed, Maybe<string> passphrase)>> RequestSensitiveData(WalletId walletId)
    {
        if (walletId == WalletAppService.SingleWalletId)
        {
            return (_seed, _passphrase);
        }

        return Result.Failure<(string seed, Maybe<string> passphrase)>("Invalid id");
    }
}
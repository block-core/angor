using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Impl;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Tests.Infrastructure.TestDoubles;

public class TestSensitiveWalletDataProvider : ISensitiveWalletDataProvider
{
    private readonly string seed;
    private readonly string passphrase;

    public TestSensitiveWalletDataProvider(string seed, string passphrase)
    {
        this.seed = seed;
        this.passphrase = passphrase;
    }

    public  Task<Result<(string seed, Maybe<string> passphrase)>> RequestSensitiveData(WalletId walletId)
    {
        return Task.FromResult(Result.Success((seed, Maybe<string>.From(passphrase))));
    }

    public void SetSensitiveData(WalletId id, (string seed, Maybe<string> passphrase) data)
    {
        throw new NotImplementedException();
    }

    public void RemoveSensitiveData(WalletId id)
    {
    }
}
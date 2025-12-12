using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Integration;

public class SeedwordsProvider(ISensitiveWalletDataProvider sensitiveWalletDataProvider) : ISeedwordsProvider
{
    public Task<Result<(string Words, Maybe<string> Passphrase)>> GetSensitiveData(string walletId)
    {
        return sensitiveWalletDataProvider.RequestSensitiveData(new WalletId(walletId));
    }
}
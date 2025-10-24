using Angor.Contests.CrossCutting;
using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Integration.WalletFunding;

public class SeedwordsProvider(ISensitiveWalletDataProvider sensitiveWalletDataProvider) : ISeedwordsProvider
{
    public Task<Result<(string Words, Maybe<string> Passphrase)>> GetSensitiveData(Guid walletId)
    {
        return sensitiveWalletDataProvider.RequestSensitiveData(new WalletId(walletId));
    }
}
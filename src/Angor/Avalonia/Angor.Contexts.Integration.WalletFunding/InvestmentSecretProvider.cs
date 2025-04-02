using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding;
using Angor.Shared;
using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Integration.WalletFunding;

public class SeedwordsProvider(IDerivationOperations derivationOperations, ISensitiveWalletDataProvider sensitiveWalletDataProvider) : ISeedwordsProvider
{
    public Task<Result<string>> InvestorKey(Guid walletId, string founderKey)
    {
        return sensitiveWalletDataProvider
            .RequestSensitiveData(new WalletId(walletId))
            .MapTry(sensData => DeriveInvestorKey(founderKey, sensData));
    }

    private string DeriveInvestorKey(string founderKey, (string seed, Maybe<string> passphrase) sensData)
    {
        return derivationOperations.DeriveInvestorKey(sensData.ToWalletWords(), founderKey);
    }
    
    public Task<Result<(string Words, Maybe<string> Passphrase)>> GetSensitiveData(Guid walletId)
    {
        return sensitiveWalletDataProvider.RequestSensitiveData(new WalletId(walletId));
    }
}
using Angor.Projects.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Wallet.Domain;
using Angor.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Integration.WalletProject;

public class InvestorKeyProvider(IDerivationOperations derivationOperations, ISensitiveWalletDataProvider sensitiveWalletDataProvider) : IInvestorKeyProvider
{
    public Task<Result<string>> InvestorKey(Guid walletId, string founderKey)
    {
        return sensitiveWalletDataProvider
            .RequestSensitiveData(new WalletId(walletId))
            .MapTry(sensData => DeriveInvestorKey(founderKey, sensData));
    }

    private string DeriveInvestorKey(string founderKey, (string seed, Maybe<string> passphrase) sensData)
    {
        var walletWords = new WalletWords()
        {
            Words = sensData.seed,
            Passphrase = sensData.passphrase.GetValueOrDefault(""),
        };
        
        return derivationOperations.DeriveInvestorKey(walletWords, founderKey);
    }
}
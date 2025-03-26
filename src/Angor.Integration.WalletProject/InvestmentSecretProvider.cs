using Angor.Projects.Infrastructure.Interfaces;
using Angor.Wallet.Domain;
using Angor.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Integration.WalletProject;

public class SensibleDataProvider : ISensibleDataProvider
{
    private readonly ISensitiveWalletDataProvider sensitiveWalletDataProvider;

    public SensibleDataProvider(ISensitiveWalletDataProvider sensitiveWalletDataProvider)
    {
        this.sensitiveWalletDataProvider = sensitiveWalletDataProvider;
    }

    public Task<Result<(string seed, Maybe<string> passphrase)>> GetSecrets(Guid walletId)
    {
        return sensitiveWalletDataProvider.RequestSensitiveData(new WalletId(walletId));
    }
}
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Application;
using Angor.Sdk.Wallet.Domain;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Sdk.Wallet.Operations;

public static class RestoreStoredWallet
{
    public record RestoreStoredWalletRequest(string WalletId, string WalletName, BitcoinNetwork Network)
        : IRequest<Result<RestoreStoredWalletResponse>>;

    public record RestoreStoredWalletResponse(WalletId Id);

    public class RestoreStoredWalletHandler(
        ISeedwordsProvider seedwordsProvider,
        IWalletAppService walletAppService)
        : IRequestHandler<RestoreStoredWalletRequest, Result<RestoreStoredWalletResponse>>
    {
        public async Task<Result<RestoreStoredWalletResponse>> Handle(
            RestoreStoredWalletRequest request, CancellationToken cancellationToken)
        {
            var sensitiveResult = await seedwordsProvider.GetSensitiveData(request.WalletId);
            if (sensitiveResult.IsFailure)
                return Result.Failure<RestoreStoredWalletResponse>(sensitiveResult.Error);

            var (seedWords, passphrase) = sensitiveResult.Value;
            if (string.IsNullOrWhiteSpace(seedWords))
                return Result.Failure<RestoreStoredWalletResponse>("Decrypted wallet contains no seed words.");

            var createResult = await walletAppService.CreateWallet(
                request.WalletName,
                seedWords,
                passphrase,
                request.Network);

            return createResult.Map(id => new RestoreStoredWalletResponse(id));
        }
    }
}

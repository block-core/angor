using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Sdk.Wallet.Operations;

public static class GetStoredWallets
{
    public record GetStoredWalletsRequest : IRequest<Result<GetStoredWalletsResponse>>;

    public record GetStoredWalletsResponse(IEnumerable<StoredWalletSummary> Wallets);

    public record StoredWalletSummary(string Id);

    public class GetStoredWalletsHandler(IWalletStore walletStore)
        : IRequestHandler<GetStoredWalletsRequest, Result<GetStoredWalletsResponse>>
    {
        public async Task<Result<GetStoredWalletsResponse>> Handle(
            GetStoredWalletsRequest request, CancellationToken cancellationToken)
        {
            var result = await walletStore.GetAll();
            return result.Map(wallets =>
                new GetStoredWalletsResponse(wallets.Select(w => new StoredWalletSummary(w.Id))));
        }
    }
}

using Angor.Sdk.Common;
using Angor.Sdk.Funding.Shared;
using Angor.Shared;
using NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;
using Nostr.Client.Utils;

namespace Angor.Sdk.Funding.Founder.Operations;

public static class GetFounderNsec
{
    public record GetFounderNsecRequest(WalletId WalletId, string FounderKey) : IRequest<Result<GetFounderNsecResponse>>;

    public record GetFounderNsecResponse(string Nsec, string Hex);

    public class GetFounderNsecHandler(
        ISeedwordsProvider seedwordsProvider,
        IDerivationOperations derivationOperations
    ) : IRequestHandler<GetFounderNsecRequest, Result<GetFounderNsecResponse>>
    {
        public async Task<Result<GetFounderNsecResponse>> Handle(GetFounderNsecRequest request, CancellationToken cancellationToken)
        {
            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(request.WalletId.Value);

            if (sensitiveDataResult.IsFailure)
                return Result.Failure<GetFounderNsecResponse>(sensitiveDataResult.Error);

            var nostrPrivateKey = await derivationOperations.DeriveProjectNostrPrivateKeyAsync(
                sensitiveDataResult.Value.ToWalletWords(),
                request.FounderKey);

            var privateKeyHex = Encoders.Hex.EncodeData(nostrPrivateKey.ToBytes());

            // Convert to nsec format using Nostr.Client library
            var nsec = NostrConverter.ToNsec(privateKeyHex);

            return Result.Success(new GetFounderNsecResponse(nsec, privateKeyHex));
        }
    }
}

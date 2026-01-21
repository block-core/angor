using Angor.Sdk.Common;
using Angor.Sdk.Funding.Shared;
using Angor.Shared;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;
using Nostr.Client.Utils;

namespace Angor.Sdk.Funding.Investor.Operations;

public static class GetInvestorNsec
{
    public record GetInvestorNsecRequest(WalletId WalletId, string FounderKey) : IRequest<Result<GetInvestorNsecResponse>>;

    public record GetInvestorNsecResponse(string Nsec);

    public class GetInvestorNsecHandler(
        ISeedwordsProvider seedwordsProvider,
        IDerivationOperations derivationOperations
    ) : IRequestHandler<GetInvestorNsecRequest, Result<GetInvestorNsecResponse>>
    {
        public async Task<Result<GetInvestorNsecResponse>> Handle(GetInvestorNsecRequest request, CancellationToken cancellationToken)
        {
            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(request.WalletId.Value);
            
            if (sensitiveDataResult.IsFailure)
                return Result.Failure<GetInvestorNsecResponse>(sensitiveDataResult.Error);

            var nostrPrivateKey = await derivationOperations.DeriveProjectNostrPrivateKeyAsync(
                sensitiveDataResult.Value.ToWalletWords(), 
                request.FounderKey);

            var privateKeyHex = Encoders.Hex.EncodeData(nostrPrivateKey.ToBytes());
            
            // Convert to nsec format using Nostr.Client library
            var nsec = NostrConverter.ToNsec(privateKeyHex);

            return Result.Success(new GetInvestorNsecResponse(nsec));
        }
    }
}

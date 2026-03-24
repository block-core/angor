using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Data.Documents.Interfaces;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Sdk.Funding.Investor.Operations;

public static class GetTotalInvested
{
    public record GetTotalInvestedRequest(WalletId WalletId) : IRequest<Result<GetTotalInvestedResponse>>;

    public record GetTotalInvestedResponse(long TotalInvestedSats);

    /// <summary>
    /// Reads investment records directly from local storage (no password required).
    /// Only returns data that has already been cached locally.
    /// </summary>
    public class GetTotalInvestedHandler(IGenericDocumentCollection<InvestmentRecordsDocument> documentCollection)
        : IRequestHandler<GetTotalInvestedRequest, Result<GetTotalInvestedResponse>>
    {
        public async Task<Result<GetTotalInvestedResponse>> Handle(GetTotalInvestedRequest request, CancellationToken cancellationToken)
        {
            var localDoc = await documentCollection.FindByIdAsync(request.WalletId.Value);

            if (localDoc.IsFailure || localDoc.Value is null)
                return Result.Success(new GetTotalInvestedResponse(0));

            var investments = localDoc.Value.Investments;
            if (investments == null || !investments.Any())
                return Result.Success(new GetTotalInvestedResponse(0));

            var totalSats = investments.Sum(r => r.InvestedAmountSats);

            return Result.Success(new GetTotalInvestedResponse(totalSats));
        }
    }
}

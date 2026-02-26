using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Services;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;
using Angor.Sdk.Funding.Projects;

namespace Angor.Sdk.Funding.Investor.Operations;

public static class CheckForReleaseSignatures
{
    public record CheckForReleaseSignaturesRequest(WalletId WalletId, ProjectId ProjectId) : IRequest<Result<CheckForReleaseSignaturesResponse>>;

    public record CheckForReleaseSignaturesResponse(bool HasReleaseSignatures);

    public class CheckForReleaseSignaturesHandler(
        ISeedwordsProvider provider,
        IDerivationOperations derivationOperations,
        IProjectService projectService,
        IPortfolioService investmentService,
        ISignService signService)
        : IRequestHandler<CheckForReleaseSignaturesRequest, Result<CheckForReleaseSignaturesResponse>>
    {
        public async Task<Result<CheckForReleaseSignaturesResponse>> Handle(CheckForReleaseSignaturesRequest request, CancellationToken cancellationToken)
        {
            var project = await projectService.GetAsync(request.ProjectId);
            if (project.IsFailure)
                return Result.Failure<CheckForReleaseSignaturesResponse>(project.Error);

            var investments = await investmentService.GetByWalletId(request.WalletId.Value);
            if (investments.IsFailure)
                return Result.Failure<CheckForReleaseSignaturesResponse>(investments.Error);

            var investment = investments.Value.ProjectIdentifiers
                .FirstOrDefault(p => p.ProjectIdentifier == request.ProjectId.Value);
            if (investment is null)
                return Result.Failure<CheckForReleaseSignaturesResponse>("No investment found for this project");

            if (string.IsNullOrEmpty(investment.RequestEventId))
                return Result.Success(new CheckForReleaseSignaturesResponse(false));

            var words = await provider.GetSensitiveData(request.WalletId.Value);
            if (words.IsFailure)
                return Result.Failure<CheckForReleaseSignaturesResponse>(words.Error);

            var investorNostrPubKey = derivationOperations.DeriveNostrPubKey(words.Value.ToWalletWords(), project.Value.FounderKey);
            var projectNostrPubKey = project.Value.NostrPubKey;

            var tcs = new TaskCompletionSource<bool>();

            signService.LookupReleaseSigs(
                investorNostrPubKey,
                projectNostrPubKey,
                null,
                investment.RequestEventId,
                _ => tcs.TrySetResult(true),
                () => tcs.TrySetResult(false));

            var hasSignatures = await tcs.Task;

            return Result.Success(new CheckForReleaseSignaturesResponse(hasSignatures));
        }
    }
}

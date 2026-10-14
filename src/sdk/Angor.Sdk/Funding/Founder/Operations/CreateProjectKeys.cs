using System.Threading;
using System.Threading.Tasks;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Data.Documents.Interfaces;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Funding.Founder.Operations;

public static class CreateProjectKeys
{
    public sealed record CreateProjectKeysRequest(WalletId WalletId) : IRequest<Result<CreateProjectKeysResponse>>;

    public sealed record CreateProjectKeysResponse(ProjectSeedDto ProjectSeedDto);

    internal sealed class CreateProjectKeysHandler(
        IGenericDocumentCollection<DerivedProjectKeys> derivedProjectKeysCollection,
        IAngorIndexerService angorIndexerService,
        ILogger<CreateProjectKeysHandler> logger)
        : IRequestHandler<CreateProjectKeysRequest, Result<CreateProjectKeysResponse>>
    {
        public async Task<Result<CreateProjectKeysResponse>> Handle(CreateProjectKeysRequest request, CancellationToken cancellationToken)
        {
            var storedKeysResult = await derivedProjectKeysCollection.FindByIdAsync(request.WalletId.ToString());
            if (storedKeysResult.IsFailure || storedKeysResult.Value is null)
            {
                logger.LogDebug("Project keys not found for WalletId {WalletId}", request.WalletId);
                return Result.Failure<CreateProjectKeysResponse>("Project keys not found. Load founder projects first.");
            }

            FounderKeys? available = null;
            foreach (var fk in storedKeysResult.Value.Keys)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                var existingResult = await Result.Try(() => angorIndexerService.GetProjectByIdAsync(fk.ProjectIdentifier));
                if (existingResult.IsFailure)
                {
                    // Not knowing whether a slot is used is not the same as the slot being free.
                    logger.LogWarning("Could not check project slot {ProjectId}: {Error}", fk.ProjectIdentifier, existingResult.Error);
                    return Result.Failure<CreateProjectKeysResponse>(
                        $"Could not check which project slots are free. The indexer may be unavailable: {existingResult.Error}");
                }

                if (existingResult.Value != null) continue;
                available = fk;
                break;
            }

            if (available is null)
            {
                logger.LogDebug("No free project slot for WalletId {WalletId}", request.WalletId);
                return Result.Failure<CreateProjectKeysResponse>("No available project slot.");
            }

            return Result.Success(new CreateProjectKeysResponse(new ProjectSeedDto(
                available.FounderKey,
                available.FounderRecoveryKey,
                available.NostrPubKey,
                available.ProjectIdentifier)));
        }
    }
}

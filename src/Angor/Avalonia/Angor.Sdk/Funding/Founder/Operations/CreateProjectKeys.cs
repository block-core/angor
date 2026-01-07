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
                var existing = await angorIndexerService.GetProjectByIdAsync(fk.ProjectIdentifier);
                if (existing != null) continue;
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

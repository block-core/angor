// File: `Angor/Contexts/Funding/Founder/Operations/StartNewProject.cs`
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

public static class CreateProjectNewKeys
{
    public sealed record CreateProjectNewKeysRequest(WalletId WalletId) : IRequest<Result<ProjectSeedDto>>;

    internal sealed class FindNextAvailableProjectKeysHandler(
        IGenericDocumentCollection<DerivedProjectKeys> derivedProjectKeysCollection,
        IAngorIndexerService angorIndexerService,
        ILogger<FindNextAvailableProjectKeysHandler> logger)
        : IRequestHandler<CreateProjectNewKeysRequest, Result<ProjectSeedDto>>
    {
        public async Task<Result<ProjectSeedDto>> Handle(CreateProjectNewKeysRequest request, CancellationToken cancellationToken)
        {
            var storedKeysResult = await derivedProjectKeysCollection.FindByIdAsync(request.WalletId.ToString());
            if (storedKeysResult.IsFailure || storedKeysResult.Value is null)
            {
                logger.LogDebug("Project keys not found for WalletId {WalletId}", request.WalletId);
                return Result.Failure<ProjectSeedDto>("Project keys not found. Load founder projects first.");
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
                return Result.Failure<ProjectSeedDto>("No available project slot.");
            }

            return Result.Success(new ProjectSeedDto(
                available.FounderKey,
                available.FounderRecoveryKey,
                available.NostrPubKey,
                available.ProjectIdentifier));
        }
    }
}

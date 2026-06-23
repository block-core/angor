using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Data.Documents.Interfaces;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;

namespace Angor.Sdk.Funding.Founder.Operations;

/// <summary>
/// Scans all 15 derived project key slots for a wallet via the indexer/Nostr,
/// discovers any projects not yet in the local founder projects database,
/// persists newly found projects locally, and returns all founder projects.
/// This is the "heavy" operation — call it explicitly (e.g. from a Scan/Refresh button).
/// </summary>
public static class ScanFounderProjects
{
    public record ScanFounderProjectsRequest(WalletId WalletId) : IRequest<Result<ScanFounderProjectsResponse>>;

    public record ScanFounderProjectsResponse(IEnumerable<ProjectDto> Projects);

    public class ScanFounderProjectsHandler(
        IProjectService projectService,
        IFounderProjectsService founderProjectsService,
        IGenericDocumentCollection<DerivedProjectKeys> derivedProjectKeysCollection,
        ILogger<ScanFounderProjectsHandler> logger) : IRequestHandler<ScanFounderProjectsRequest, Result<ScanFounderProjectsResponse>>
    {
        public async Task<Result<ScanFounderProjectsResponse>> Handle(ScanFounderProjectsRequest request, CancellationToken cancellationToken)
        {
            // Step 1: Get all 15 derived key slots for this wallet
            var keysResult = await derivedProjectKeysCollection.FindByIdAsync(request.WalletId.Value);

            if (keysResult.IsFailure || keysResult.Value == null)
                return Result.Failure<ScanFounderProjectsResponse>(
                    keysResult.IsFailure ? keysResult.Error : "No derived keys found for the given wallet.");

            var allDerivedIds = keysResult.Value.Keys
                .Select(k => k.ProjectIdentifier)
                .ToHashSet();

            // Step 2: Get already-known project IDs from local DB
            var localResult = await founderProjectsService.GetByWalletId(request.WalletId.Value);
            var knownIds = localResult.IsSuccess
                ? localResult.Value.Select(r => r.ProjectIdentifier).ToHashSet()
                : new HashSet<string>();

            // Step 3: Determine which derived keys we haven't checked yet
            var unknownIds = allDerivedIds.Except(knownIds).Select(id => new ProjectId(id)).ToArray();

            // Step 4: Query the indexer/Nostr for the unknown keys to see if they exist on-chain
            var newRecords = new List<FounderProjectRecord>();
            string? scanError = null;
            if (unknownIds.Length > 0)
            {
                var scanResult = await projectService.GetAllAsync(unknownIds);
                if (scanResult.IsSuccess)
                {
                    foreach (var project in scanResult.Value)
                    {
                        newRecords.Add(new FounderProjectRecord
                        {
                            ProjectIdentifier = project.Id.Value
                        });
                    }
                }
                else
                {
                    scanError = scanResult.Error;
                    logger.LogWarning(
                        "Failed to discover projects from relay for wallet {WalletId}: {Error}",
                        request.WalletId.Value, scanResult.Error);
                }
            }

            // Step 5: Persist any newly discovered projects
            if (newRecords.Count > 0)
            {
                await founderProjectsService.AddRange(request.WalletId.Value, newRecords);
            }

            // Step 6: Load all known projects (local + newly discovered) and return full DTOs
            var allKnownResult = await founderProjectsService.GetByWalletId(request.WalletId.Value);
            var allKnownIds = allKnownResult.IsSuccess
                ? allKnownResult.Value.Select(r => new ProjectId(r.ProjectIdentifier)).ToArray()
                : Array.Empty<ProjectId>();

            if (allKnownIds.Length == 0)
            {
                // No local projects either — propagate the scan error if there was one
                if (scanError != null)
                    return Result.Failure<ScanFounderProjectsResponse>(scanError);

                return Result.Success(new ScanFounderProjectsResponse(Enumerable.Empty<ProjectDto>()));
            }

            var projects = await projectService.GetAllAsync(allKnownIds);

            if (projects.IsFailure)
                return Result.Failure<ScanFounderProjectsResponse>(projects.Error);

            var dtoList = projects.Value
                .OrderByDescending(p => p.StartingDate)
                .Select(p => p.ToDto());

            return Result.Success(new ScanFounderProjectsResponse(dtoList));
        }
    }
}

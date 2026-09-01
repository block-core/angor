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
        Wallet.Infrastructure.Interfaces.ISensitiveWalletDataProvider sensitiveWalletDataProvider,
        Wallet.Infrastructure.Interfaces.IWalletFactory walletFactory,
        ILogger<ScanFounderProjectsHandler> logger) : IRequestHandler<ScanFounderProjectsRequest, Result<ScanFounderProjectsResponse>>
    {
        public async Task<Result<ScanFounderProjectsResponse>> Handle(ScanFounderProjectsRequest request, CancellationToken cancellationToken)
        {
            // Step 1: Get all 15 derived key slots for this wallet
            var keysResult = await derivedProjectKeysCollection.FindByIdAsync(request.WalletId.Value);

            if (keysResult.IsFailure || keysResult.Value == null)
                return Result.Failure<ScanFounderProjectsResponse>(
                    keysResult.IsFailure ? keysResult.Error : "No derived keys found for the given wallet.");

            // Self-heal: builds affected by the ARM64 JIT derivation-collapse bug
            // (docs/ai-docs/taproot-arm64-jit-bug.md) persisted key sets where every
            // slot carries the same project identifier. Re-derive from the seed
            // (OS-secured key store, no prompt) and persist the corrected set.
            var distinctIds = keysResult.Value.Keys.Select(k => k.ProjectIdentifier).Distinct().Count();
            if (distinctIds < keysResult.Value.Keys.Count)
            {
                logger.LogWarning(
                    "ScanFounderProjects: wallet {WalletId} has collapsed derived keys ({Slots} slots, {Distinct} distinct ids) — rebuilding from seed",
                    request.WalletId.Value, keysResult.Value.Keys.Count, distinctIds);

                var sensitiveDataResult = await sensitiveWalletDataProvider.RequestSensitiveData(request.WalletId);
                if (sensitiveDataResult.IsFailure)
                    return Result.Failure<ScanFounderProjectsResponse>(
                        $"Derived project keys are corrupted and the seed is unavailable to rebuild them: {sensitiveDataResult.Error}");

                var rebuildResult = await walletFactory.RebuildFounderKeysAsync(
                    sensitiveDataResult.Value.ToWalletWords(), request.WalletId);
                if (rebuildResult.IsFailure)
                    return Result.Failure<ScanFounderProjectsResponse>(
                        $"Failed to rebuild derived project keys: {rebuildResult.Error}");

                keysResult = await derivedProjectKeysCollection.FindByIdAsync(request.WalletId.Value);
                if (keysResult.IsFailure || keysResult.Value == null)
                    return Result.Failure<ScanFounderProjectsResponse>("Derived keys missing after rebuild.");

                logger.LogInformation(
                    "ScanFounderProjects: rebuilt derived keys for wallet {WalletId} — now {Distinct} distinct project ids",
                    request.WalletId.Value,
                    keysResult.Value.Keys.Select(k => k.ProjectIdentifier).Distinct().Count());
            }

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

            logger.LogInformation(
                "ScanFounderProjects: wallet {WalletId} — {DerivedCount} derived slots, {KnownCount} known locally, {UnknownCount} to scan. Derived ids: {DerivedIds}",
                request.WalletId.Value, allDerivedIds.Count, knownIds.Count, unknownIds.Length,
                string.Join(", ", allDerivedIds));

            // Diagnostic for the ARM64 JIT derivation-collapse bug: all 15 slots deriving
            // to the same key means BIP32/Angor-key derivation is broken on this runtime.
            var storedKeys = keysResult.Value.Keys;
            var distinctFounderKeys = storedKeys.Select(k => k.FounderKey).Distinct().Count();
            var distinctNostr = storedKeys.Select(k => k.NostrPubKey).Distinct().Count();
            if (allDerivedIds.Count < storedKeys.Count || distinctFounderKeys < storedKeys.Count)
            {
                logger.LogError(
                    "ScanFounderProjects: DERIVATION COLLAPSE for wallet {WalletId} — {SlotCount} slots but only {DistinctIds} distinct project ids, {DistinctFounderKeys} distinct founder keys, {DistinctNostr} distinct nostr keys. First founder keys: {Keys}",
                    request.WalletId.Value, storedKeys.Count, allDerivedIds.Count, distinctFounderKeys, distinctNostr,
                    string.Join(", ", storedKeys.Take(3).Select(k => $"[{k.Index}]{k.FounderKey}")));
            }

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

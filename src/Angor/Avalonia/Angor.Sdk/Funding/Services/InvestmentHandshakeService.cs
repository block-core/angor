using Angor.Sdk.Common;
using Angor.Sdk.Funding.Shared;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Serilog;

namespace Angor.Sdk.Funding.Services;

/// <summary>
/// Service implementation for managing investment Handshakes
/// Combines LookupInvestmentRequestsAsync and LookupInvestmentRequestApprovals
/// </summary>
public class InvestmentHandshakeService(
    IAngorDocumentDatabase database,
    ISignService signService,
    INostrDecrypter nostrDecrypter,
    ISerializer serializer,
    ILogger logger)
    : IInvestmentHandshakeService
{
    private readonly IDocumentCollection<InvestmentHandshake> _collection = database.GetCollection<InvestmentHandshake>();

    public Task<Result<IEnumerable<InvestmentHandshake>>> GetHandshakesAsync(WalletId walletId, ProjectId projectId)
    {
        return _collection.FindAsync(c =>
            c.WalletId == walletId.Value && 
            c.ProjectId == projectId.Value);
    }

    public async Task<Result<InvestmentHandshake?>> GetHandshakeByRequestEventIdAsync(
        WalletId walletId, 
        ProjectId projectId, 
        string requestEventId)
    {
        var result = await _collection.FindAsync(c =>
            c.WalletId == walletId.Value &&
            c.ProjectId == projectId.Value &&
            c.RequestEventId == requestEventId);

        return result.Map(Handshakes => Handshakes.FirstOrDefault());
    }

    public async Task<Result> UpsertHandshakeAsync(InvestmentHandshake Handshake)
    {
        try
        {
            Handshake.UpdatedAt = DateTime.UtcNow;
            var result = await _collection.UpsertAsync(Handshake);
            return result.IsSuccess
                ? Result.Success()
                : Result.Failure(result.Error);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to upsert Handshake {RequestEventId}", Handshake.RequestEventId);
            return Result.Failure($"Failed to upsert Handshake: {ex.Message}");
        }
    }

    public async Task<Result> UpsertHandshakesAsync(IEnumerable<InvestmentHandshake> Handshakes)
    {
        try
        {
            var HandshakeList = Handshakes.ToList();
            foreach (var Handshake in HandshakeList)
            {
                Handshake.UpdatedAt = DateTime.UtcNow;
            }

            var tasks = HandshakeList.Select(c => _collection.UpsertAsync(c));
            var results = await Task.WhenAll(tasks);

            var failures = results.Where(r => r.IsFailure).ToList();
            if (failures.Any())
            {
                var errors = string.Join(", ", failures.Select(f => f.Error));
                logger.Error("Failed to upsert some Handshakes: {Errors}", errors);
                return Result.Failure($"Failed to upsert some Handshakes: {errors}");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to upsert Handshakes");
            return Result.Failure($"Failed to upsert Handshakes: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<InvestmentHandshake>>> SyncHandshakesFromNostrAsync(
        WalletId walletId,
        ProjectId projectId,
        string projectNostrPubKey)
    {
        try
        {
            logger.Information("Starting sync of investment Handshakes for project {ProjectId}", projectId.Value);

            // Fetch all investment requests from Nostr
            var requestsResult = await FetchInvestmentRequestsFromNostr(projectNostrPubKey);
            if (requestsResult.IsFailure)
            {
                logger.Error("Failed to fetch investment requests: {Error}", requestsResult.Error);
                return Result.Failure<IEnumerable<InvestmentHandshake>>(requestsResult.Error);
            }

            var requests = requestsResult.Value.ToList();
            logger.Information("Fetched {Count} investment requests", requests.Count);

            // Fetch all approvals from Nostr
            var approvalsResult = await FetchInvestmentApprovalsFromNostr(projectNostrPubKey);
            if (approvalsResult.IsFailure)
            {
                logger.Warning("Failed to fetch approvals: {Error}", approvalsResult.Error);
            }

            var approvals = approvalsResult.IsSuccess 
                ? approvalsResult.Value.ToDictionary(a => a.EventIdentifier, a => a)
                : new Dictionary<string, ApprovalInfo>();

            logger.Information("Fetched {Count} investment approvals", approvals.Count);

            // Get existing Handshakes from database
            var existingResult = await _collection.FindAsync(c =>
                c.WalletId == walletId.Value &&
                c.ProjectId == projectId.Value);

            var existingHandshakes = existingResult.IsSuccess
                ? existingResult.Value.ToDictionary(c => c.RequestEventId, c => c)
                : new Dictionary<string, InvestmentHandshake>();

            var HandshakesToUpsert = new List<InvestmentHandshake>();

            // Process each request
            foreach (var request in requests)
            {
                // Check if we already have this Handshake
                if (existingHandshakes.TryGetValue(request.Id, out var existingHandshake))
                {
                    // Update existing Handshake if approval status changed
                    if (approvals.TryGetValue(request.Id, out var approval))
                    {
                        if (existingHandshake.ApprovalEventId != approval.EventIdentifier)
                        {
                            existingHandshake.ApprovalEventId = approval.EventIdentifier;
                            existingHandshake.ApprovalCreated = approval.Created;
                            existingHandshake.Status = InvestmentRequestStatus.Approved;
                            HandshakesToUpsert.Add(existingHandshake);
                        }
                    }
                    continue;
                }
                
                SignRecoveryRequest? recoveryRequest = null;

                try
                {
                    var decryptResult = await nostrDecrypter.Decrypt(walletId, projectId, request);
                    if (decryptResult.IsSuccess)
                    {
                        recoveryRequest = serializer.Deserialize<SignRecoveryRequest>(decryptResult.Value);
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "Failed to decrypt/parse request {EventId}", request.Id);
                }

                // Check if this request has an approval
                var hasApproval = approvals.TryGetValue(request.Id, out var requestApproval);

                // Create Handshake object
                var Handshake = new InvestmentHandshake
                {
                    Id = GenerateCompositeId(walletId, projectId, request.Id),
                    WalletId = walletId.Value,
                    ProjectId = projectId.Value,
                    RequestEventId = request.Id,
                    InvestorNostrPubKey = request.SenderNostrPubKey,
                    RequestCreated = request.Created,
                    ProjectIdentifier = recoveryRequest?.ProjectIdentifier,
                    InvestmentTransactionHex = recoveryRequest?.InvestmentTransactionHex,
                    UnfundedReleaseAddress = recoveryRequest?.UnfundedReleaseAddress,
                    UnfundedReleaseKey = recoveryRequest?.UnfundedReleaseKey,
                    ApprovalEventId = hasApproval ? requestApproval.EventIdentifier : null,
                    ApprovalCreated = hasApproval ? requestApproval.Created : null,
                    Status = hasApproval ? InvestmentRequestStatus.Approved : InvestmentRequestStatus.Pending,
                    IsSynced = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                HandshakesToUpsert.Add(Handshake);
            }

            // Store all Handshakes
            if (HandshakesToUpsert.Any())
            {
                var upsertResult = await UpsertHandshakesAsync(HandshakesToUpsert);
                if (upsertResult.IsFailure)
                {
                    logger.Warning("Failed to store some Handshakes: {Error}", upsertResult.Error);
                }
            }

            logger.Information("Synced {Count} investment Handshakes for project {ProjectId}",
                HandshakesToUpsert.Count, projectId.Value);

            return Result.Success<IEnumerable<InvestmentHandshake>>(HandshakesToUpsert);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to sync Handshakes from Nostr");
            return Result.Failure<IEnumerable<InvestmentHandshake>>($"Failed to sync Handshakes: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<InvestmentHandshake>>> GetHandshakesByStatusAsync(
        WalletId walletId,
        ProjectId projectId,
        InvestmentRequestStatus status)
    {
        return await _collection.FindAsync(c =>
            c.WalletId == walletId.Value &&
            c.ProjectId == projectId.Value &&
            c.Status == status);
    }

    private static string GenerateCompositeId(WalletId walletId, ProjectId projectId, string requestEventId)
    {
        var composite = $"{walletId.Value}|{projectId.Value}|{requestEventId}";
        var hashBytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(composite));
        return Convert.ToHexString(hashBytes);
    }

    private async Task<Result<IEnumerable<DirectMessage>>> FetchInvestmentRequestsFromNostr(string projectNostrPubKey)
    {
        try
        {
            var tcs = new TaskCompletionSource<List<DirectMessage>>();
            var messages = new List<DirectMessage>();

            await signService.LookupInvestmentRequestsAsync(
                projectNostrPubKey,
                null,
                null,
                (id, pubKey, content, created) => messages.Add(new DirectMessage(id, pubKey, content, created)),
                () => tcs.SetResult(messages)
            );

            var result = await tcs.Task;
            return Result.Success<IEnumerable<DirectMessage>>(result);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to fetch investment requests from Nostr");
            return Result.Failure<IEnumerable<DirectMessage>>($"Failed to fetch investment requests: {ex.Message}");
        }
    }

    private async Task<Result<IEnumerable<ApprovalInfo>>> FetchInvestmentApprovalsFromNostr(string projectNostrPubKey)
    {
        try
        {
            var tcs = new TaskCompletionSource<List<ApprovalInfo>>();
            var approvals = new List<ApprovalInfo>();

            signService.LookupInvestmentRequestApprovals(
                projectNostrPubKey,
                (profileIdentifier, created, eventIdentifier) => 
                    approvals.Add(new ApprovalInfo(profileIdentifier, created, eventIdentifier)),
                () => tcs.SetResult(approvals)
            );

            var result = await tcs.Task;
            return Result.Success<IEnumerable<ApprovalInfo>>(result);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to fetch investment approvals from Nostr");
            return Result.Failure<IEnumerable<ApprovalInfo>>($"Failed to fetch approvals: {ex.Message}");
        }
    }

    private record ApprovalInfo(string ProfileIdentifier, DateTime Created, string EventIdentifier);
}


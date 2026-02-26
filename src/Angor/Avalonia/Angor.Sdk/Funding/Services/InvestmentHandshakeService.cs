using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Shared;
using Angor.Data.Documents.Interfaces;
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
    ISignService signService,
    INostrDecrypter nostrDecrypter,
    ISerializer serializer,
    ILogger logger,
    IGenericDocumentCollection<InvestmentHandshake> collection)
    : IInvestmentHandshakeService
{
    //private readonly IGenericDocumentCollection<InvestmentHandshake> _collection ;//= database.GetCollection<>();

    public Task<Result<IEnumerable<InvestmentHandshake>>> GetHandshakesAsync(WalletId walletId, ProjectId projectId)
    {
        return collection.FindAsync(c =>
            c.WalletId == walletId.Value && 
            c.ProjectId == projectId.Value);
    }

    public async Task<Result<InvestmentHandshake?>> GetHandshakeByRequestEventIdAsync(
        WalletId walletId, 
        ProjectId projectId, 
        string requestEventId)
    {
        var id = GenerateCompositeId(walletId, projectId, requestEventId);

        // NOTE: We use FindAsync(c => c.Id == id) instead of FindByIdAsync(id) because
        // FindByIdAsync queries Document<T>.Id (the wrapper's Id) while FindAsync queries
        // against InvestmentHandshake.Id (Data.Id). Due to a caching bug in
        // LiteDbGenericDocumentCollection.UpsertAsync (the getDocumentIdProperty ??= caching
        // compiles only the first closure, so batch upserts store incorrect wrapper Ids),
        // the wrapper Id may be wrong. InvestmentHandshake.Id is always set correctly.
        var res = await collection.FindAsync(c => c.Id == id);

        return res.Map(items => items.FirstOrDefault());
    }
    

    public async Task<Result> UpsertHandshakesAsync(IEnumerable<InvestmentHandshake> handshakes)
    {
        try
        {
            var tasks = handshakes.Select(h =>
            {
                var id = GenerateCompositeId(new WalletId(h.WalletId), new ProjectId(h.ProjectId), h.RequestEventId);
                return collection.UpsertAsync(item => id, h);
            });
            
            var results = await Task.WhenAll(tasks);

            var failures = results.Where(r => r.IsFailure).ToList();
            if (failures.Count == 0)
                return Result.Success();

            var errors = string.Join(", ", failures.Select(f => f.Error));
            logger.Error("Failed to upsert some Handshakes: {Errors}", errors);
            return Result.Failure($"Failed to upsert some Handshakes: {errors}");

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

            // Fetch all investment-related messages in a single call
            var messagesResult = await FetchAllInvestmentMessagesFromNostr(projectNostrPubKey);
            if (messagesResult.IsFailure)
            {
                logger.Error("Failed to fetch investment messages: {Error}", messagesResult.Error);
                return Result.Failure<IEnumerable<InvestmentHandshake>>(messagesResult.Error);
            }

            (List<DirectMessage> requests, List<DirectMessage> notifications, List<DirectMessage> cancellations, List<ApprovalInfo> approvals) = messagesResult.Value;
            
            logger.Information("Fetched {RequestCount} requests, {NotificationCount} notifications, {CancellationCount} cancellations, {ApprovalCount} approvals",
                requests.Count, notifications.Count, cancellations.Count, approvals.Count);

            // Parse cancellations into a lookup dictionary by RequestEventId
            var cancellationLookup = new Dictionary<string, CancellationNotification>();
            foreach (var cancellation in cancellations)
            {
                try
                {
                    var cancelNotification = serializer.Deserialize<CancellationNotification>(cancellation.Content);
                    if (cancelNotification != null && !string.IsNullOrEmpty(cancelNotification.RequestEventId))
                    {
                        cancellationLookup[cancelNotification.RequestEventId] = cancelNotification;
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "Failed to parse cancellation {EventId}", cancellation.Id);
                }
            }

            // Build approvals lookup by event identifier
            var approvalsLookup = approvals.ToDictionary(a => a.EventIdentifier, a => a);

            // Get existing Handshakes from database
            var existingResult = await collection.FindAsync(c =>
                c.WalletId == walletId.Value &&
                c.ProjectId == projectId.Value);

            var existingHandshakes = existingResult.IsSuccess
                ? existingResult.Value.ToDictionary(c => c.RequestEventId, c => c)
                : new Dictionary<string, InvestmentHandshake>();

            var handshakesToUpsert = new List<InvestmentHandshake>();

            // Process each request
            foreach (var request in requests)
            {
                // Check if we already have this Handshake
                if (existingHandshakes.TryGetValue(request.Id, out var existingHandshake))
                {
                    var needsUpdate = false;
                    
                    // Always check and update approval fields if available
                    if (approvalsLookup.TryGetValue(request.Id, out var approval))
                    {
                        if (existingHandshake.ApprovalEventId != approval.EventIdentifier)
                        {
                            existingHandshake.ApprovalEventId = approval.EventIdentifier;
                            existingHandshake.ApprovalCreated = approval.Created;
                            if (existingHandshake.Status == InvestmentRequestStatus.Pending)
                            {
                                existingHandshake.Status = InvestmentRequestStatus.Approved;
                            }
                            needsUpdate = true;
                        }
                    }
                    
                    // Apply cancellation status last (takes precedence over approval)
                    if (cancellationLookup.ContainsKey(request.Id) && existingHandshake.Status != InvestmentRequestStatus.Cancelled)
                    {
                        existingHandshake.Status = InvestmentRequestStatus.Cancelled;
                        needsUpdate = true;
                    }
                    
                    if (needsUpdate)
                    {
                        existingHandshake.UpdatedAt = DateTime.UtcNow;
                        handshakesToUpsert.Add(existingHandshake);
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
                var hasApproval = approvalsLookup.TryGetValue(request.Id, out var requestApproval);
                
                // Check if this request has been cancelled
                var isCancelled = cancellationLookup.ContainsKey(request.Id);
                
                // Determine the status: cancelled takes precedence, then approved, then pending
                var status = isCancelled ? InvestmentRequestStatus.Cancelled 
                    : hasApproval ? InvestmentRequestStatus.Approved : InvestmentRequestStatus.Pending;

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
                    Status = status,
                    IsSynced = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                handshakesToUpsert.Add(Handshake);
            }

            // Process each notification (direct investments below threshold)
            foreach (var notification in notifications)
            {
                // Check if we already have this Handshake
                if (existingHandshakes.TryGetValue(notification.Id, out _))
                {
                    continue;
                }
                
                InvestmentNotification? investmentNotification = null;

                try
                {
                    var decryptResult = await nostrDecrypter.Decrypt(walletId, projectId, notification);
                    if (decryptResult.IsSuccess)
                    {
                        investmentNotification = serializer.Deserialize<InvestmentNotification>(decryptResult.Value);
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "Failed to decrypt/parse notification {EventId}", notification.Id);
                }

                // Create Handshake object for direct investment
                handshakesToUpsert.Add(new InvestmentHandshake
                {
                    Id = GenerateCompositeId(walletId, projectId, notification.Id),
                    WalletId = walletId.Value,
                    ProjectId = projectId.Value,
                    RequestEventId = notification.Id,
                    InvestorNostrPubKey = notification.SenderNostrPubKey,
                    RequestCreated = notification.Created,
                    ProjectIdentifier = investmentNotification?.ProjectIdentifier,
                    InvestmentTransactionId = investmentNotification?.TransactionId,
                    InvestmentTransactionHex = null, // Not sent in notification, can be fetched from indexer if needed
                    UnfundedReleaseAddress = null, // Not applicable for direct investments
                    UnfundedReleaseKey = null,
                    ApprovalEventId = null, // Direct investments don't need approval
                    ApprovalCreated = null,
                    Status = InvestmentRequestStatus.Invested, // Already invested
                    IsSynced = true,
                    IsDirectInvestment = true, // Mark as direct investment
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }


            // Store all Handshakes
            if (handshakesToUpsert.Any())
            {
                var upsertResult = await UpsertHandshakesAsync(handshakesToUpsert);
                if (upsertResult.IsFailure)
                {
                    logger.Warning("Failed to store some Handshakes: {Error}", upsertResult.Error);
                }
            }

            logger.Information("Synced {Count} investment Handshakes for project {ProjectId}",
                handshakesToUpsert.Count, projectId.Value);

            return Result.Success<IEnumerable<InvestmentHandshake>>(handshakesToUpsert);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to sync Handshakes from Nostr");
            return Result.Failure<IEnumerable<InvestmentHandshake>>($"Failed to sync Handshakes: {ex.Message}");
        }
    }

    private static string GenerateCompositeId(WalletId walletId, ProjectId projectId, string requestEventId)
    {
        var composite = $"{walletId.Value}|{projectId.Value}|{requestEventId}";
        var hashBytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(composite));
        return Convert.ToHexString(hashBytes);
    }

    private async Task<Result<(List<DirectMessage> Requests, List<DirectMessage> Notifications, List<DirectMessage> Cancellations, List<ApprovalInfo> Approvals)>> 
        FetchAllInvestmentMessagesFromNostr(string projectNostrPubKey)
    {
        try
        {
            var tcs = new TaskCompletionSource<bool>();
            
            var requests = new List<DirectMessage>();
            var notifications = new List<DirectMessage>();
            var cancellations = new List<DirectMessage>();
            var approvals = new List<ApprovalInfo>();

            await signService.LookupAllInvestmentMessagesAsync(
                projectNostrPubKey,
                null,
                null,
                (messageType, id, pubKey, content, created) =>
                {
                    switch (messageType)
                    {
                        case InvestmentMessageType.Request:
                            requests.Add(new DirectMessage(id, pubKey, content, created));
                            break;
                        case InvestmentMessageType.Notification:
                            notifications.Add(new DirectMessage(id, pubKey, content, created));
                            break;
                        case InvestmentMessageType.Cancellation:
                            cancellations.Add(new DirectMessage(id, pubKey, content, created));
                            break;
                        case InvestmentMessageType.Approval:
                            // For approvals, we need to extract the event identifier from the content/tags
                            // The pubKey here is the founder's pubKey, and we need the referenced event ID
                            approvals.Add(new ApprovalInfo(pubKey, created, id));
                            break;
                    }
                },
                () => tcs.SetResult(true)
            );

            await tcs.Task;
            
            return Result.Success((
                requests.DistinctBy(r => r.Id).ToList(),
                notifications.DistinctBy(n => n.Id).ToList(),
                cancellations.DistinctBy(c => c.Id).ToList(),
                approvals.DistinctBy(a => a.EventIdentifier).ToList()));
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to fetch investment messages from Nostr");
            return Result.Failure<(List<DirectMessage>, List<DirectMessage>, List<DirectMessage>, List<ApprovalInfo>)>(
                $"Failed to fetch investment messages: {ex.Message}");
        }
    }

    private record ApprovalInfo(string ProfileIdentifier, DateTime Created, string EventIdentifier);
}


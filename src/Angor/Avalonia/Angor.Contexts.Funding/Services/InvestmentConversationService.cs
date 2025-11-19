using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Shared;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Serilog;

namespace Angor.Contexts.Funding.Services;

/// <summary>
/// Service implementation for managing investment conversations
/// Combines LookupInvestmentRequestsAsync and LookupInvestmentRequestApprovals
/// </summary>
public class InvestmentConversationService(
    IAngorDocumentDatabase database,
    ISignService signService,
    INostrDecrypter nostrDecrypter,
    ISerializer serializer,
    ILogger logger)
    : IInvestmentConversationService
{
    private readonly IDocumentCollection<InvestmentConversation> _collection = database.GetCollection<InvestmentConversation>();

    public Task<Result<IEnumerable<InvestmentConversation>>> GetConversationsAsync(WalletId walletId, ProjectId projectId)
    {
        return _collection.FindAsync(c =>
            c.WalletId == walletId.Value && 
            c.ProjectId == projectId.Value);
    }

    public async Task<Result<InvestmentConversation?>> GetConversationByRequestEventIdAsync(
        WalletId walletId, 
        ProjectId projectId, 
        string requestEventId)
    {
        var result = await _collection.FindAsync(c =>
            c.WalletId == walletId.Value &&
            c.ProjectId == projectId.Value &&
            c.RequestEventId == requestEventId);

        return result.Map(conversations => conversations.FirstOrDefault());
    }

    public async Task<Result> UpsertConversationAsync(InvestmentConversation conversation)
    {
        try
        {
            conversation.UpdatedAt = DateTime.UtcNow;
            var result = await _collection.UpsertAsync(conversation);
            return result.IsSuccess
                ? Result.Success()
                : Result.Failure(result.Error);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to upsert conversation {RequestEventId}", conversation.RequestEventId);
            return Result.Failure($"Failed to upsert conversation: {ex.Message}");
        }
    }

    public async Task<Result> UpsertConversationsAsync(IEnumerable<InvestmentConversation> conversations)
    {
        try
        {
            var conversationList = conversations.ToList();
            foreach (var conversation in conversationList)
            {
                conversation.UpdatedAt = DateTime.UtcNow;
            }

            var tasks = conversationList.Select(c => _collection.UpsertAsync(c));
            var results = await Task.WhenAll(tasks);

            var failures = results.Where(r => r.IsFailure).ToList();
            if (failures.Any())
            {
                var errors = string.Join(", ", failures.Select(f => f.Error));
                logger.Error("Failed to upsert some conversations: {Errors}", errors);
                return Result.Failure($"Failed to upsert some conversations: {errors}");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to upsert conversations");
            return Result.Failure($"Failed to upsert conversations: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<InvestmentConversation>>> SyncConversationsFromNostrAsync(
        WalletId walletId,
        ProjectId projectId,
        string projectNostrPubKey)
    {
        try
        {
            logger.Information("Starting sync of investment conversations for project {ProjectId}", projectId.Value);

            // Fetch all investment requests from Nostr
            var requestsResult = await FetchInvestmentRequestsFromNostr(projectNostrPubKey);
            if (requestsResult.IsFailure)
            {
                logger.Error("Failed to fetch investment requests: {Error}", requestsResult.Error);
                return Result.Failure<IEnumerable<InvestmentConversation>>(requestsResult.Error);
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

            // Get existing conversations from database
            var existingResult = await _collection.FindAsync(c =>
                c.WalletId == walletId.Value &&
                c.ProjectId == projectId.Value);

            var existingConversations = existingResult.IsSuccess
                ? existingResult.Value.ToDictionary(c => c.RequestEventId, c => c)
                : new Dictionary<string, InvestmentConversation>();

            var conversationsToUpsert = new List<InvestmentConversation>();

            // Process each request
            foreach (var request in requests)
            {
                // Check if we already have this conversation
                if (existingConversations.TryGetValue(request.Id, out var existingConversation))
                {
                    // Update existing conversation if approval status changed
                    if (approvals.TryGetValue(request.Id, out var approval))
                    {
                        if (existingConversation.ApprovalEventId != approval.EventIdentifier)
                        {
                            existingConversation.ApprovalEventId = approval.EventIdentifier;
                            existingConversation.ApprovalCreated = approval.Created;
                            existingConversation.Status = InvestmentRequestStatus.Approved;
                            conversationsToUpsert.Add(existingConversation);
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

                // Create conversation object
                var conversation = new InvestmentConversation
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

                conversationsToUpsert.Add(conversation);
            }

            // Store all conversations
            if (conversationsToUpsert.Any())
            {
                var upsertResult = await UpsertConversationsAsync(conversationsToUpsert);
                if (upsertResult.IsFailure)
                {
                    logger.Warning("Failed to store some conversations: {Error}", upsertResult.Error);
                }
            }

            logger.Information("Synced {Count} investment conversations for project {ProjectId}",
                conversationsToUpsert.Count, projectId.Value);

            return Result.Success<IEnumerable<InvestmentConversation>>(conversationsToUpsert);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to sync conversations from Nostr");
            return Result.Failure<IEnumerable<InvestmentConversation>>($"Failed to sync conversations: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<InvestmentConversation>>> GetConversationsByStatusAsync(
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


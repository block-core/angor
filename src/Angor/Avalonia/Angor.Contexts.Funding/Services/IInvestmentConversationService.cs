using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Shared;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Funding.Services;

/// <summary>
/// Service interface for managing investment conversations (requests and approvals)
/// </summary>
public interface IInvestmentConversationService
{
    /// <summary>
    /// Get all investment conversations for a specific wallet and project
    /// </summary>
    Task<Result<IEnumerable<InvestmentConversation>>> GetConversationsAsync(WalletId walletId, ProjectId projectId);
    
    /// <summary>
    /// Get a specific investment conversation by request event ID
    /// </summary>
    Task<Result<InvestmentConversation?>> GetConversationByRequestEventIdAsync(WalletId walletId, ProjectId projectId, string requestEventId);
    
    /// <summary>
    /// Store or update an investment conversation
    /// </summary>
    Task<Result> UpsertConversationAsync(InvestmentConversation conversation);
    
    /// <summary>
    /// Store or update multiple investment conversations
    /// </summary>
    Task<Result> UpsertConversationsAsync(IEnumerable<InvestmentConversation> conversations);
    
    /// <summary>
    /// Sync investment conversations from Nostr (both requests and approvals) and store in DB
    /// </summary>
    Task<Result<IEnumerable<InvestmentConversation>>> SyncConversationsFromNostrAsync(
        WalletId walletId, 
        ProjectId projectId,
        string projectNostrPubKey);
    
    /// <summary>
    /// Get conversations by status
    /// </summary>
    Task<Result<IEnumerable<InvestmentConversation>>> GetConversationsByStatusAsync(
        WalletId walletId, 
        ProjectId projectId, 
        InvestmentRequestStatus status);
}


using Angor.Sdk.Common;
using Angor.Sdk.Funding.Shared;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Funding.Services;

/// <summary>
/// Service interface for managing investment Handshakes (requests and approvals)
/// </summary>
public interface IInvestmentHandshakeService
{
    /// <summary>
    /// Get all investment Handshakes for a specific wallet and project
    /// </summary>
    Task<Result<IEnumerable<InvestmentHandshake>>> GetHandshakesAsync(WalletId walletId, ProjectId projectId);
    
    /// <summary>
    /// Get a specific investment Handshake by request event ID
    /// </summary>
    Task<Result<InvestmentHandshake?>> GetHandshakeByRequestEventIdAsync(WalletId walletId, ProjectId projectId, string requestEventId);
    
    /// <summary>
    /// Store or update an investment Handshake
    /// </summary>
    Task<Result> UpsertHandshakeAsync(InvestmentHandshake Handshake);
    
    /// <summary>
    /// Store or update multiple investment Handshakes
    /// </summary>
    Task<Result> UpsertHandshakesAsync(IEnumerable<InvestmentHandshake> Handshakes);
    
    /// <summary>
    /// Sync investment Handshakes from Nostr (both requests and approvals) and store in DB
    /// </summary>
    Task<Result<IEnumerable<InvestmentHandshake>>> SyncHandshakesFromNostrAsync(
        WalletId walletId, 
        ProjectId projectId,
        string projectNostrPubKey);
    
    /// <summary>
    /// Get Handshakes by status
    /// </summary>
    Task<Result<IEnumerable<InvestmentHandshake>>> GetHandshakesByStatusAsync(
        WalletId walletId, 
        ProjectId projectId, 
        InvestmentRequestStatus status);
}


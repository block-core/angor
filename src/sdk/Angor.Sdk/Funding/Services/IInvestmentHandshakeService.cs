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
    /// Sync investment Handshakes from Nostr (both requests and approvals) and store in DB
    /// </summary>
    Task<Result<IEnumerable<InvestmentHandshake>>> SyncHandshakesFromNostrAsync(
        WalletId walletId, 
        ProjectId projectId,
        string projectNostrPubKey);
}


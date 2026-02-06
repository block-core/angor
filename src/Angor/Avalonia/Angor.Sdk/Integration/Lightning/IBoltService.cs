using Angor.Sdk.Integration.Lightning.Models;
using CSharpFunctionalExtensions;

namespace Angor.Sdk.Integration.Lightning;

/// <summary>
/// Service for interacting with Bolt Lightning API
/// </summary>
public interface IBoltService
{
    /// <summary>
    /// Creates a new Lightning wallet for a user
    /// </summary>
    Task<Result<BoltWallet>> CreateWalletAsync(string userId);
    
    /// <summary>
    /// Gets an existing wallet by wallet ID
    /// </summary>
    Task<Result<BoltWallet>> GetWalletAsync(string walletId);
    
    /// <summary>
    /// Gets wallet balance
    /// </summary>
    Task<Result<long>> GetWalletBalanceAsync(string walletId);
    
    /// <summary>
    /// Creates a Lightning invoice for receiving funds
    /// </summary>
    Task<Result<BoltInvoice>> CreateInvoiceAsync(string walletId, long amountSats, string memo);
    
    /// <summary>
    /// Gets invoice details and payment status
    /// </summary>
    Task<Result<BoltInvoice>> GetInvoiceAsync(string invoiceId);
    
    /// <summary>
    /// Gets payment status for an invoice
    /// </summary>
    Task<Result<BoltPaymentStatus>> GetPaymentStatusAsync(string invoiceId);
    
    /// <summary>
    /// Pays a Lightning invoice (bolt11)
    /// </summary>
    Task<Result<BoltPayment>> PayInvoiceAsync(string walletId, string bolt11Invoice);
    
    /// <summary>
    /// Lists all invoices for a wallet
    /// </summary>
    Task<Result<List<BoltInvoice>>> ListInvoicesAsync(string walletId, int limit = 100);
    
    /// <summary>
    /// Gets the on-chain Bitcoin address for a Lightning wallet to swap funds
    /// </summary>
    Task<Result<string>> GetSwapAddressAsync(string walletId, long amountSats);
}


using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Integration.Lightning;
using Angor.Sdk.Integration.Lightning.Models;
using Angor.Sdk.Wallet.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Integration.Lightning.Examples;

/// <summary>
/// Example usage of Boltz submarine swaps for funding investments via Lightning.
/// This demonstrates the direct swap flow without intermediate custody.
/// </summary>
public class LightningFundingExamples
{
    private readonly IInvestmentAppService _investmentAppService;
    private readonly IBoltzSwapService _boltzSwapService;
    private readonly ILogger<LightningFundingExamples> _logger;

    public LightningFundingExamples(
        IInvestmentAppService investmentAppService,
        IBoltzSwapService boltzSwapService,
        ILogger<LightningFundingExamples> logger)
    {
        _investmentAppService = investmentAppService;
        _boltzSwapService = boltzSwapService;
        _logger = logger;
    }

    /// <summary>
    /// Example 1: Simple flow - Create swap and monitor
    /// </summary>
    public async Task<bool> SimpleSwapFlowExample(
        WalletId walletId, 
        ProjectId projectId, 
        long amountSats,
        string receivingAddress)
    {
        _logger.LogInformation("=== Example 1: Simple Boltz Submarine Swap ===");

        // Step 1: Create the swap
        var createRequest = new CreateLightningSwapForInvestment.CreateLightningSwapRequest(
            WalletId: walletId,
            ProjectId: projectId,
            InvestmentAmount: new Amount(amountSats),
            ReceivingAddress: receivingAddress
        );

        var createResult = await _investmentAppService.CreateLightningSwap(createRequest);

        if (createResult.IsFailure)
        {
            _logger.LogError("✗ Failed to create swap: {Error}", createResult.Error);
            return false;
        }

        var swap = createResult.Value.Swap;

        _logger.LogInformation("✓ Swap created successfully!");
        _logger.LogInformation("  Swap ID: {SwapId}", swap.Id);
        _logger.LogInformation("  Lightning Invoice: {Invoice}", swap.Invoice);
        _logger.LogInformation("  Invoice Amount: {Amount} sats", swap.InvoiceAmount);
        _logger.LogInformation("  Expected on-chain: {Amount} sats", swap.ExpectedAmount);

        // Display the invoice to the user (QR code, copy button, etc.)
        _logger.LogInformation("");
        _logger.LogInformation(">>> PAY THIS INVOICE: {Invoice}", swap.Invoice);
        _logger.LogInformation("");

        // Step 2: Monitor the swap and claim funds
        var monitorRequest = new MonitorLightningSwap.MonitorLightningSwapRequest(
            WalletId: walletId,
            SwapId: swap.Id,
            Timeout: TimeSpan.FromMinutes(30)
        );

        var monitorResult = await _investmentAppService.MonitorLightningSwap(monitorRequest);

        if (monitorResult.IsFailure)
        {
            _logger.LogError("✗ Swap failed: {Error}", monitorResult.Error);
            return false;
        }

        _logger.LogInformation("✓ Swap claimed!");
        _logger.LogInformation("  Claim Transaction ID: {TxId}", monitorResult.Value.ClaimTransactionId);
        _logger.LogInformation("  Now monitor the receiving address for funds to arrive...");

        return true;
    }

    /// <summary>
    /// Example 2: Create swap only (for async monitoring later)
    /// </summary>
    public async Task<string?> CreateSwapOnlyExample(
        WalletId walletId,
        ProjectId projectId,
        long amountSats,
        string receivingAddress)
    {
        _logger.LogInformation("=== Example 2: Create Swap Only ===");

        var request = new CreateLightningSwapForInvestment.CreateLightningSwapRequest(
            WalletId: walletId,
            ProjectId: projectId,
            InvestmentAmount: new Amount(amountSats),
            ReceivingAddress: receivingAddress
        );

        var result = await _investmentAppService.CreateLightningSwap(request);

        if (result.IsSuccess)
        {
            var swap = result.Value.Swap;
            
            _logger.LogInformation("✓ Swap created!");
            _logger.LogInformation("  Swap ID: {SwapId} (save this for monitoring)", swap.Id);
            _logger.LogInformation("  Invoice: {Invoice}", swap.Invoice);
            _logger.LogInformation("  Timeout block: {Block}", swap.TimeoutBlockHeight);
            
            return swap.Id;
        }
        else
        {
            _logger.LogError("✗ Failed: {Error}", result.Error);
            return null;
        }
    }

    /// <summary>
    /// Example 4: Monitor existing swap by ID
    /// </summary>
    public async Task<bool> MonitorExistingSwapExample(
        WalletId walletId,
        string swapId)
    {
        _logger.LogInformation("=== Example 4: Monitor Existing Swap ===");

        var request = new MonitorLightningSwap.MonitorLightningSwapRequest(
            WalletId: walletId,
            SwapId: swapId
        );

        var result = await _investmentAppService.MonitorLightningSwap(request);

        if (result.IsSuccess)
        {
            var status = result.Value.SwapStatus;
            _logger.LogInformation("✓ Swap status: {Status}", status.Status);
            _logger.LogInformation("  Claim Transaction: {TxId}", result.Value.ClaimTransactionId ?? "pending");
            return status.Status.IsComplete();
        }
        else
        {
            _logger.LogError("✗ Failed: {Error}", result.Error);
            return false;
        }
    }

    /// <summary>
    /// Complete end-to-end example with error handling
    /// </summary>
    public async Task CompleteExampleWithErrorHandling(
        WalletId walletId,
        ProjectId projectId,
        long amountSats,
        string receivingAddress)
    {
        _logger.LogInformation("=== Complete End-to-End Boltz Swap Example ===");

        try
        {
            // Validate amount
            if (amountSats < 10000)
            {
                _logger.LogError("Amount too small (minimum ~10,000 sats for Boltz)");
                return;
            }

            // Create and monitor swap
            var success = await SimpleSwapFlowExample(walletId, projectId, amountSats, receivingAddress);

            if (success)
            {
                _logger.LogInformation("========================================");
                _logger.LogInformation("SUCCESS! Lightning funds are now on-chain.");
                _logger.LogInformation("Use the detected UTXOs to build your investment transaction.");
                _logger.LogInformation("========================================");
            }
            else
            {
                _logger.LogError("========================================");
                _logger.LogError("FAILED. Check the logs for details.");
                _logger.LogError("========================================");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error");
        }
    }
}


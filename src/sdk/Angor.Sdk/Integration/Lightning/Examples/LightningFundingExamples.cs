using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Wallet.Domain;
using Angor.Shared.Integration.Lightning;
using Angor.Shared.Integration.Lightning.Models;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Integration.Lightning.Examples;

/// <summary>
/// Example usage of Boltz submarine swaps for funding via Lightning.
/// Uses the generic CreateLightningSwap handler with an address-derived claim key.
/// Works for both invest and deploy flows.
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
        string claimPublicKey,
        long amountSats,
        string receivingAddress)
    {
        _logger.LogInformation("=== Example 1: Simple Boltz Submarine Swap ===");

        // Step 1: Create the swap with the address-derived claim key
        var createRequest = new CreateLightningSwap.CreateLightningSwapRequest(
            WalletId: walletId,
            ClaimPublicKey: claimPublicKey,
            Amount: new Amount(amountSats),
            ReceivingAddress: receivingAddress,
            StageCount: 4
        );

        var createResult = await _investmentAppService.CreateLightningSwap(createRequest);

        if (createResult.IsFailure)
        {
            _logger.LogError("Failed to create swap: {Error}", createResult.Error);
            return false;
        }

        var swap = createResult.Value.Swap;

        _logger.LogInformation("Swap created. ID: {SwapId}, Invoice: {Invoice}",
            swap.Id, swap.Invoice);

        // Step 2: Monitor the swap and claim funds
        var monitorRequest = new MonitorLightningSwap.MonitorLightningSwapRequest(
            WalletId: walletId,
            SwapId: swap.Id,
            Timeout: TimeSpan.FromMinutes(30)
        );

        var monitorResult = await _investmentAppService.MonitorLightningSwap(monitorRequest);

        if (monitorResult.IsFailure)
        {
            _logger.LogError("Swap failed: {Error}", monitorResult.Error);
            return false;
        }

        _logger.LogInformation("Swap claimed. TxId: {TxId}", monitorResult.Value.ClaimTransactionId);
        return true;
    }

    /// <summary>
    /// Example 2: Create swap only (for async monitoring later)
    /// </summary>
    public async Task<string?> CreateSwapOnlyExample(
        WalletId walletId,
        string claimPublicKey,
        long amountSats,
        string receivingAddress)
    {
        _logger.LogInformation("=== Example 2: Create Swap Only ===");

        var request = new CreateLightningSwap.CreateLightningSwapRequest(
            WalletId: walletId,
            ClaimPublicKey: claimPublicKey,
            Amount: new Amount(amountSats),
            ReceivingAddress: receivingAddress,
            StageCount: 4
        );

        var result = await _investmentAppService.CreateLightningSwap(request);

        if (result.IsSuccess)
        {
            var swap = result.Value.Swap;
            _logger.LogInformation("Swap created. ID: {SwapId}, Invoice: {Invoice}", swap.Id, swap.Invoice);
            return swap.Id;
        }

        _logger.LogError("Failed: {Error}", result.Error);
        return null;
    }

    /// <summary>
    /// Example 3: Monitor existing swap by ID
    /// </summary>
    public async Task<bool> MonitorExistingSwapExample(
        WalletId walletId,
        string swapId)
    {
        _logger.LogInformation("=== Example 3: Monitor Existing Swap ===");

        var request = new MonitorLightningSwap.MonitorLightningSwapRequest(
            WalletId: walletId,
            SwapId: swapId
        );

        var result = await _investmentAppService.MonitorLightningSwap(request);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Swap status: {Status}, Claim TX: {TxId}",
                result.Value.SwapStatus.Status, result.Value.ClaimTransactionId ?? "pending");
            return result.Value.SwapStatus.Status.IsComplete();
        }

        _logger.LogError("Failed: {Error}", result.Error);
        return false;
    }
}

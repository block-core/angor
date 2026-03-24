using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Angor.Sdk.Funding.Investor.Operations;

/// <summary>
/// Handler for monitoring a specific address for incoming funds.
/// This is used when an investor wants to fund an investment from an external wallet by sending funds to a generated address.
/// </summary>
public static class MonitorAddressForFunds
{
    /// <summary>
    /// Request to monitor an address for incoming funds.
    /// </summary>
    /// <param name="WalletId">The wallet ID that owns the address</param>
    /// <param name="Address">The address to monitor for incoming funds</param>
    /// <param name="RequiredAmount">The minimum amount in satoshis required</param>
    /// <param name="Timeout">Maximum time to wait for funds (if null, uses default timeout)</param>
    public record MonitorAddressForFundsRequest(
        WalletId WalletId,
        string Address,
        Amount RequiredAmount,
        TimeSpan? Timeout = null) : IRequest<Result<MonitorAddressForFundsResponse>>;

    /// <summary>
    /// Response containing the detected UTXOs on the monitored address.
    /// </summary>
    /// <param name="DetectedUtxos">List of UTXOs detected on the address</param>
    /// <param name="TotalAmount">Total amount in satoshis detected</param>
    /// <param name="Address">The address that was monitored</param>
    public record MonitorAddressForFundsResponse(
        List<UtxoData> DetectedUtxos,
        Amount TotalAmount,
        string Address);

    public class MonitorAddressForFundsHandler(
        IMempoolMonitoringService mempoolMonitoringService,
        IWalletAccountBalanceService walletAccountBalanceService,
        ILogger<MonitorAddressForFundsHandler> logger)
        : IRequestHandler<MonitorAddressForFundsRequest, Result<MonitorAddressForFundsResponse>>
    {
        // Default timeout for monitoring - hardcoded for now, will be moved to configuration later
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);

        public async Task<Result<MonitorAddressForFundsResponse>> Handle(
            MonitorAddressForFundsRequest request, 
            CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation(
                    "Starting address monitoring for wallet {WalletId}, address {Address}, required amount: {Amount} sats",
                    request.WalletId.Value, request.Address, request.RequiredAmount.Sats);

                // Validate the address belongs to the wallet
                var accountBalanceResult = await walletAccountBalanceService.GetAccountBalanceInfoAsync(request.WalletId);
                if (accountBalanceResult.IsFailure)
                {
                    logger.LogWarning("Failed to get account balance for wallet {WalletId}: {Error}", 
                        request.WalletId.Value, accountBalanceResult.Error);
                    return Result.Failure<MonitorAddressForFundsResponse>(accountBalanceResult.Error);
                }

                var accountInfo = accountBalanceResult.Value.AccountInfo;
                
                // Verify the address belongs to this wallet
                var addressInfo = accountInfo.AllAddresses()
                    .FirstOrDefault(a => a.Address == request.Address);

                if (addressInfo == null)
                {
                    logger.LogWarning("Address {Address} not found in wallet {WalletId}", 
                        request.Address, request.WalletId.Value);
                    return Result.Failure<MonitorAddressForFundsResponse>(
                        $"Address {request.Address} not found in wallet. Please ensure the address belongs to this wallet.");
                }

                // Use provided timeout or default
                var timeout = request.Timeout ?? DefaultTimeout;

                logger.LogInformation(
                    "Monitoring address {Address} for {Timeout} minutes or until {Amount} sats are received",
                    request.Address, timeout.TotalMinutes, request.RequiredAmount.Sats);

                // Monitor the address for incoming funds
                var detectedUtxos = await mempoolMonitoringService.MonitorAddressForFundsAsync(
                    request.Address,
                    request.RequiredAmount.Sats,
                    timeout,
                    cancellationToken);

                if (!detectedUtxos.Any())
                {
                    logger.LogWarning("No UTXOs detected on address {Address}", request.Address);
                    return Result.Failure<MonitorAddressForFundsResponse>(
                        $"No funds detected on address {request.Address} within the timeout period.");
                }

                var totalAmount = detectedUtxos.Sum(u => u.value);

                logger.LogInformation(
                    "Successfully detected {Count} UTXO(s) on address {Address}, total: {TotalAmount} sats",
                    detectedUtxos.Count, request.Address, totalAmount);

                // Update account info with the new UTXOs
                var addedCount = accountInfo.AddNewUtxos(request.Address, detectedUtxos);
                logger.LogDebug("Added {AddedCount} new UTXO(s) to address {Address}", addedCount, request.Address);

                // Save the updated account info
                var saveResult = await SaveAccountInfo(request.WalletId, accountBalanceResult.Value);
                if (saveResult.IsFailure)
                {
                    logger.LogWarning("Failed to save account info: {Error}", saveResult.Error);
                    // Don't fail the request, just log the warning - the UTXOs were detected successfully
                }

                return Result.Success(new MonitorAddressForFundsResponse(
                    detectedUtxos,
                    new Amount(totalAmount),
                    request.Address));
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Address monitoring cancelled for address {Address}", request.Address);
                return Result.Failure<MonitorAddressForFundsResponse>("Address monitoring was cancelled.");
            }
            catch (TimeoutException ex)
            {
                logger.LogWarning(ex, "Timeout monitoring address {Address}", request.Address);
                return Result.Failure<MonitorAddressForFundsResponse>(ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error monitoring address {Address}", request.Address);
                return Result.Failure<MonitorAddressForFundsResponse>($"Error monitoring address: {ex.Message}");
            }
        }

        private async Task<Result> SaveAccountInfo(WalletId walletId, AccountBalanceInfo accountBalanceInfo)
        {
            try
            {
                var result = await walletAccountBalanceService.SaveAccountBalanceInfoAsync(walletId, accountBalanceInfo);
                
                if (result.IsSuccess)
                {
                    logger.LogDebug("Account info saved successfully for wallet {WalletId}", walletId.Value);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error saving account info for wallet {WalletId}", walletId.Value);
                return Result.Failure($"Error saving account info: {ex.Message}");
            }
        }
    }
}


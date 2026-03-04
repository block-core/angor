<<<<<<<< HEAD:src/Angor/Shared/Integration/Lightning/IBoltzClaimService.cs
using Angor.Shared.Integration.Lightning.Models;
using CSharpFunctionalExtensions;

namespace Angor.Shared.Integration.Lightning;

/// <summary>
/// Service for claiming funds from Boltz reverse submarine swaps.
/// This encapsulates all the Taproot/MuSig2 claim logic for Boltz swaps.
/// </summary>
public interface IBoltzClaimService
{
    /// <summary>
    /// Claims funds from a completed reverse submarine swap.
    /// Attempts cooperative MuSig2 claim first, falls back to script path spend.
    /// </summary>
    Task<Result<BoltzClaimResult>> ClaimSwapAsync(
        BoltzSubmarineSwap swap,
        string claimPrivateKeyHex,
        string lockupTransactionHex,
        int lockupOutputIndex = 0,
        long feeRate = 2);
}

/// <summary>Result of a successful swap claim</summary>
public record BoltzClaimResult(
    string ClaimTransactionId,
    string ClaimTransactionHex);

========
// Moved to Angor.Shared.Integration.Lightning — see LightningGlobalUsings.cs
>>>>>>>> 2f47cc51 (Refactor Boltz integration: move models and services to Angor.Shared.Integration.Lightning namespace):src/Angor/Avalonia/Angor.Sdk/Integration/Lightning/IBoltzClaimService.cs

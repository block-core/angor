using Angor.Sdk.Common;
using CSharpFunctionalExtensions;

namespace App.UI.Shared.PaymentFlow;

/// <summary>
/// Configuration for a reusable payment flow.
/// Each consumer (invest, deploy) provides its own config with callbacks
/// for what to do after payment and where to navigate on success.
/// </summary>
public record PaymentFlowConfig
{
    /// <summary>Amount to receive in satoshis (the net investment/deploy amount).</summary>
    public required long AmountSats { get; init; }

    /// <summary>Number of stage outputs for Lightning swap fee estimation (0 if not applicable).</summary>
    public int StageCount { get; init; }

    /// <summary>Default fee rate in sat/vB.</summary>
    public int FeeRateSatsPerVbyte { get; init; } = 20;

    /// <summary>
    /// Total on-chain amount the user must send when paying via on-chain invoice.
    /// For investment flows, this must cover the investment amount + Angor fee + miner fee,
    /// because the spending transaction is built exclusively from UTXOs on the funding address.
    /// If null, defaults to <see cref="AmountSats"/> (suitable for flows like deploy where the
    /// spending transaction is not restricted to the funding address).
    /// </summary>
    public long? OnChainRequiredSatsOverride { get; init; }

    /// <summary>
    /// Effective on-chain required amount: uses the explicit override if provided,
    /// otherwise falls back to <see cref="AmountSats"/>.
    /// </summary>
    public long OnChainRequiredSats => OnChainRequiredSatsOverride ?? AmountSats;

    /// <summary>
    /// Optional: recalculates the on-chain required amount for the actual number of UTXOs
    /// detected on the funding address. The signing code spends ALL UTXOs on the address
    /// and pays ~68 vB per input, so if the user pays in multiple transactions the required
    /// amount grows with each extra input. When set, the payment flow rechecks the received
    /// total against this after funds arrive and keeps monitoring for the shortfall instead
    /// of failing at signing. Null = no recheck (flows not restricted to the funding address).
    /// </summary>
    public Func<int, long>? OnChainRequiredForUtxoCount { get; init; }

    /// <summary>
    /// Calculates the total on-chain amount needed for an investment transaction,
    /// including the Angor fee (1%) and estimated miner fee.
    /// Use this as <see cref="OnChainRequiredSatsOverride"/> for investment flows.
    /// </summary>
    public static long EstimateOnChainRequired(long investmentAmountSats, int stageCount, int feeRateSatsPerVbyte)
        => EstimateOnChainRequired(investmentAmountSats, stageCount, feeRateSatsPerVbyte, inputCount: 1);

    /// <summary>
    /// Calculates the total on-chain amount needed for an investment transaction for a
    /// specific number of funding inputs. The signing transaction spends all UTXOs on the
    /// funding address, so each extra UTXO adds ~68 vB of miner fee.
    /// </summary>
    public static long EstimateOnChainRequired(long investmentAmountSats, int stageCount, int feeRateSatsPerVbyte, int inputCount)
    {
        const int AngorFeePercentage = 1;
        long angorFee = (investmentAmountSats * AngorFeePercentage) / 100;

        // Investment tx structure (same estimate as CreateLightningSwap):
        //   ~10.5 vB  tx overhead
        //   ~68   vB  per P2WPKH input (first input included in the 252 constant)
        //    43   vB  1 P2WSH output (angor fee)
        //   ~99   vB  1 OP_RETURN output
        //  N×43   vB  N P2TR stage outputs
        //    31   vB  1 P2WPKH change output
        // Total ≈ 252 + ((inputCount − 1) × 68) + (stageCount × 43) vbytes
        const int InputVbytes = 68;
        int extraInputs = Math.Max(0, inputCount - 1);
        int estimatedTxVbytes = 252 + (extraInputs * InputVbytes) + (stageCount * 43);
        long estimatedMinerFee = feeRateSatsPerVbyte * estimatedTxVbytes;

        return investmentAmountSats + angorFee + estimatedMinerFee;
    }

    /// <summary>Title for the invoice screen, e.g. "Pay to Invest" or "Fund Deployment".</summary>
    public string Title { get; init; } = "Payment";

    /// <summary>Title shown on the success screen, e.g. "Investment Successful".</summary>
    public required string SuccessTitle { get; init; }

    /// <summary>Description shown on the success screen.</summary>
    public string SuccessDescription { get; init; } = "";

    /// <summary>Text on the success screen button, e.g. "View My Investments" or "Go to My Projects".</summary>
    public required string SuccessButtonText { get; init; }

    /// <summary>Called when the user clicks the success button. Navigate to the appropriate page.</summary>
    public required Action OnSuccessButtonClicked { get; init; }

    /// <summary>Called when the flow is dismissed before success.</summary>
    public Action? OnDismissed { get; init; }

    /// <summary>
    /// Called after funds are received at the funding address (on-chain or Lightning claim).
    /// The consumer builds and publishes its specific transaction here
    /// (investment tx for invest, project creation tx for deploy).
    /// </summary>
    public required Func<WalletId, string, long, Task<Result>> OnPaymentReceived { get; init; }

    /// <summary>
    /// Called when the user chooses "Pay with Wallet" (direct UTXO spend).
    /// Parameters: walletId, amountSats, feeRate.
    /// Null = hide the "Pay with Wallet" button (invoice-only flow).
    /// </summary>
    public Func<WalletId, long, long, Task<Result>>? OnPayWithWallet { get; init; }

    /// <summary>
    /// If true, starts directly on the invoice screen when no wallet has enough available
    /// balance for a direct wallet payment.
    /// </summary>
    public bool SkipWalletSelectorWhenNoWalletCanPay { get; init; }
}

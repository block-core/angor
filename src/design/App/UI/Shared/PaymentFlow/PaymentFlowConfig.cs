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
    /// <summary>Amount to receive in satoshis.</summary>
    public required long AmountSats { get; init; }

    /// <summary>Number of stage outputs for Lightning swap fee estimation (0 if not applicable).</summary>
    public int StageCount { get; init; }

    /// <summary>Default fee rate in sat/vB.</summary>
    public int FeeRateSatsPerVbyte { get; init; } = 20;

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
}

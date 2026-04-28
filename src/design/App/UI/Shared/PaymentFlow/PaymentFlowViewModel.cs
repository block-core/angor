using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Wallet.Application;
using Angor.Shared.Integration.Lightning;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Wallet.Domain;
using App.UI.Shared.Services;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using MonitorOp = Angor.Sdk.Funding.Investor.Operations.MonitorAddressForFunds;

namespace App.UI.Shared.PaymentFlow;

/// <summary>Which screen the payment flow is showing.</summary>
public enum PaymentFlowScreen
{
    WalletSelector,
    Invoice,
    Success
}

/// <summary>Which payment-network tab is active inside the invoice screen.</summary>
public enum NetworkTab
{
    OnChain,
    Lightning,
    Liquid,
    Import
}

/// <summary>
/// Reusable payment flow component: Wallet Selector → Invoice (on-chain/Lightning) → Success.
/// Consumers provide a <see cref="PaymentFlowConfig"/> with callbacks for what to do after
/// payment (build invest tx, deploy project, etc.) and where to navigate on success.
/// </summary>
public partial class PaymentFlowViewModel : ReactiveObject
{
    private readonly IWalletAppService _walletAppService;
    private readonly IInvestmentAppService _investmentAppService;
    private readonly IBoltzSwapService _boltzSwapService;
    private readonly IWalletContext _walletContext;
    private readonly ICurrencyService _currencyService;
    private readonly Func<BitcoinNetwork> _getNetwork;
    private readonly ILogger _logger;
    private readonly PaymentFlowConfig _config;
    private CancellationTokenSource? _monitorCts;

    // ── State ──
    [Reactive] private PaymentFlowScreen currentScreen = PaymentFlowScreen.WalletSelector;
    [Reactive] private WalletInfo? selectedWallet;
    [Reactive] private bool isProcessing;
    [Reactive] private string paymentStatusText = "Awaiting payment...";
    [Reactive] private bool paymentReceived;
    [Reactive] private NetworkTab selectedNetworkTab = NetworkTab.OnChain;
    [Reactive] private string? onChainAddress;
    [Reactive] private string? lightningInvoice;
    [Reactive] private string? lightningSwapId;
    [Reactive] private bool isGeneratingLightningInvoice;
    [Reactive] private string? errorMessage;
    [Reactive] private long selectedFeeRate;

    // ── Derived visibility ──
    public bool IsWalletSelector => CurrentScreen == PaymentFlowScreen.WalletSelector;
    public bool IsInvoice => CurrentScreen == PaymentFlowScreen.Invoice;
    public bool IsSuccess => CurrentScreen == PaymentFlowScreen.Success;
    public bool HasSelectedWallet => SelectedWallet != null;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool ShowPayWithWallet => _config.OnPayWithWallet != null;
    public string PayButtonText => SelectedWallet != null
        ? $"Pay with {SelectedWallet.Name}"
        : "Choose Wallet";

    // ── Tab visibility ──
    public bool IsOnChainTab => SelectedNetworkTab == NetworkTab.OnChain;
    public bool IsLightningTab => SelectedNetworkTab == NetworkTab.Lightning;
    public bool IsLiquidTab => SelectedNetworkTab == NetworkTab.Liquid;
    public bool IsImportTab => SelectedNetworkTab == NetworkTab.Import;

    public string InvoiceFieldLabel => SelectedNetworkTab switch
    {
        NetworkTab.Lightning => "Lightning Invoice",
        NetworkTab.Liquid => "Liquid Address",
        NetworkTab.Import => "Imported Invoice",
        _ => "On-Chain Address"
    };

    public string InvoiceTabIcon => SelectedNetworkTab switch
    {
        NetworkTab.Lightning => "fa-solid fa-bolt",
        NetworkTab.Liquid => "fa-solid fa-droplet",
        NetworkTab.Import => "fa-solid fa-file-import",
        _ => "fa-brands fa-bitcoin"
    };

    /// <summary>Minimum Lightning swap amount from Boltz, formatted for display. Fetched on first Lightning tab switch.</summary>
    [Reactive] private string? lightningMinAmountText;

    /// <summary>The text to show in the invoice/address field (on-chain or Lightning).</summary>
    public string? InvoiceString => SelectedNetworkTab == NetworkTab.Lightning
        ? LightningInvoice
        : OnChainAddress ?? PaymentStatusText;

    /// <summary>Content for the QR code (null when no address/invoice is ready).</summary>
    public string? QrCodeContent => SelectedNetworkTab == NetworkTab.Lightning
        ? LightningInvoice
        : OnChainAddress;

    // ── Config passthrough ──
    public string Title => _config.Title;
    public string SuccessTitle => _config.SuccessTitle;
    public string SuccessDescription => _config.SuccessDescription;
    public string SuccessButtonText => _config.SuccessButtonText;
    public string CurrencySymbol => _currencyService.Symbol;
    public string AmountDisplay => $"{_config.AmountSats / 100_000_000m:F8} {_currencyService.Symbol}";

    // ── Wallets ──
    public ReadOnlyObservableCollection<WalletInfo> Wallets => _walletContext.Wallets;

    // ── Commands ──
    public ReactiveCommand<Unit, Unit> GenerateReceiveAddressCommand { get; }
    public ReactiveCommand<Unit, Unit> PayToOnChainAddressCommand { get; }
    public ReactiveCommand<Unit, Unit> PayViaLightningCommand { get; }
    public ReactiveCommand<Unit, Unit> PayWithWalletCommand { get; }

    public PaymentFlowViewModel(
        IWalletAppService walletAppService,
        IInvestmentAppService investmentAppService,
        IBoltzSwapService boltzSwapService,
        IWalletContext walletContext,
        ICurrencyService currencyService,
        Func<BitcoinNetwork> getNetwork,
        ILogger logger,
        PaymentFlowConfig config)
    {
        _walletAppService = walletAppService;
        _investmentAppService = investmentAppService;
        _boltzSwapService = boltzSwapService;
        _walletContext = walletContext;
        _currencyService = currencyService;
        _getNetwork = getNetwork;
        _logger = logger;
        _config = config;
        SelectedFeeRate = config.FeeRateSatsPerVbyte;

        // Fetch Boltz minimum for the Lightning tab label
        _ = Task.Run(async () =>
        {
            try
            {
                var fees = await boltzSwapService.GetReverseSwapFeesAsync();
                if (fees.IsSuccess)
                    LightningMinAmountText = $"Lightning min: {fees.Value.MinAmount:N0} sats";
            }
            catch { /* non-critical */ }
        });

        GenerateReceiveAddressCommand = ReactiveCommand.CreateFromTask(GenerateReceiveAddressAsync);
        GenerateReceiveAddressCommand.ThrownExceptions.Subscribe(ex =>
        {
            _logger.LogError(ex, "GenerateReceiveAddressCommand error");
            PaymentStatusText = $"Error: {ex.Message}";
        });
        PayToOnChainAddressCommand = ReactiveCommand.CreateFromTask(PayToOnChainAddressAsync);
        PayToOnChainAddressCommand.ThrownExceptions.Subscribe(ex =>
        {
            _logger.LogError(ex, "PayToOnChainAddressCommand error");
            PaymentStatusText = $"Error: {ex.Message}";
        });
        PayViaLightningCommand = ReactiveCommand.CreateFromTask(PayViaLightningAsync);
        PayViaLightningCommand.ThrownExceptions.Subscribe(ex =>
        {
            _logger.LogError(ex, "PayViaLightningCommand error");
            PaymentStatusText = $"Error: {ex.Message}";
        });
        PayWithWalletCommand = ReactiveCommand.CreateFromTask(PayWithWalletAsync);
        PayWithWalletCommand.ThrownExceptions.Subscribe(ex =>
        {
            _logger.LogError(ex, "PayWithWalletCommand error");
            PaymentStatusText = $"Error: {ex.Message}";
        });

        // Raise derived property notifications
        this.WhenAnyValue(x => x.CurrentScreen).Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(IsWalletSelector));
            this.RaisePropertyChanged(nameof(IsInvoice));
            this.RaisePropertyChanged(nameof(IsSuccess));
        });
        this.WhenAnyValue(x => x.SelectedWallet).Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(HasSelectedWallet));
            this.RaisePropertyChanged(nameof(PayButtonText));
        });
        this.WhenAnyValue(x => x.ErrorMessage).Subscribe(_ =>
            this.RaisePropertyChanged(nameof(HasError)));
        this.WhenAnyValue(x => x.SelectedNetworkTab).Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(IsOnChainTab));
            this.RaisePropertyChanged(nameof(IsLightningTab));
            this.RaisePropertyChanged(nameof(IsLiquidTab));
            this.RaisePropertyChanged(nameof(IsImportTab));
            this.RaisePropertyChanged(nameof(InvoiceFieldLabel));
            this.RaisePropertyChanged(nameof(InvoiceTabIcon));
            this.RaisePropertyChanged(nameof(InvoiceString));
            this.RaisePropertyChanged(nameof(QrCodeContent));
        });
        this.WhenAnyValue(x => x.OnChainAddress, x => x.LightningInvoice, x => x.PaymentStatusText)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(InvoiceString));
                this.RaisePropertyChanged(nameof(QrCodeContent));
            });
    }

    // ═══════════════════════════════════════════════════════════════════
    // Wallet Selection
    // ═══════════════════════════════════════════════════════════════════

    public void SelectWallet(WalletInfo wallet)
    {
        foreach (var w in Wallets) w.IsSelected = false;
        wallet.IsSelected = true;
        SelectedWallet = wallet;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Pay with Wallet (direct UTXO spend)
    // ═══════════════════════════════════════════════════════════════════

    private async Task PayWithWalletAsync()
    {
        if (_config.OnPayWithWallet == null || SelectedWallet?.Id == null)
            return;

        ErrorMessage = null;
        IsProcessing = true;
        PaymentStatusText = "Processing payment...";

        try
        {
            var result = await _config.OnPayWithWallet(
                SelectedWallet.Id, _config.AmountSats, SelectedFeeRate);

            if (result.IsFailure)
            {
                ErrorMessage = result.Error;
                IsProcessing = false;
                return;
            }

            _ = _walletContext.RefreshBalanceAsync(SelectedWallet.Id);
            CurrentScreen = PaymentFlowScreen.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayWithWalletAsync failed");
            ErrorMessage = $"Payment failed: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Invoice Flow (on-chain + Lightning)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Switch to invoice screen. Generates the receive address once, then starts on-chain monitoring.</summary>
    public void ShowInvoice()
    {
        CurrentScreen = PaymentFlowScreen.Invoice;
        SelectedNetworkTab = NetworkTab.OnChain;
        LightningInvoice = null;
        LightningSwapId = null;
        OnChainAddress = null;
        ErrorMessage = null;
        IsProcessing = true;
        PaymentStatusText = "Generating invoice address...";
        GenerateReceiveAddressCommand.Execute().Subscribe();
    }

    private async Task GenerateReceiveAddressAsync()
    {
        var wallet = Wallets.FirstOrDefault();
        if (wallet == null)
        {
            var createResult = await EnsureWalletExistsAsync();
            if (createResult.IsFailure)
            {
                ErrorMessage = createResult.Error;
                IsProcessing = false;
                return;
            }
            wallet = Wallets.FirstOrDefault();
            if (wallet == null)
            {
                ErrorMessage = "Wallet was created but not found after reload.";
                IsProcessing = false;
                return;
            }
        }
        if (wallet.Id is null || string.IsNullOrEmpty(wallet.Id.Value))
        {
            ErrorMessage = "Wallet has no ID.";
            IsProcessing = false;
            return;
        }

        PaymentStatusText = "Refreshing wallet...";
        try
        {
            await _walletContext.RefreshAllBalancesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RefreshAllBalancesAsync failed");
            ErrorMessage = $"Refresh wallet failed: {ex.Message}";
            IsProcessing = false;
            return;
        }

        PaymentStatusText = "Generating invoice address...";
        Result<Address> addressResult;
        try
        {
            addressResult = await _walletAppService.GetNextReceiveAddress(wallet.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetNextReceiveAddress threw");
            ErrorMessage = $"GetNextReceiveAddress threw: {ex.GetType().Name}: {ex.Message}";
            IsProcessing = false;
            return;
        }
        if (addressResult.IsFailure)
        {
            _logger.LogError("GetNextReceiveAddress failed: {Error}", addressResult.Error);
            ErrorMessage = $"GetNextReceiveAddress failed: {addressResult.Error}";
            IsProcessing = false;
            return;
        }

        OnChainAddress = addressResult.Value.Value;
        _logger.LogInformation("Receive address generated: {Address}", OnChainAddress);

        PayToOnChainAddress();
    }

    private async Task<Result> EnsureWalletExistsAsync()
    {
        _logger.LogInformation("No wallet found — auto-creating");
        PaymentStatusText = "Creating wallet...";

        var result = await _walletAppService.CreateWalletWithoutPassword(_getNetwork());
        if (result.IsFailure)
        {
            _logger.LogError("Auto-create wallet failed: {Error}", result.Error);
            return Result.Failure(result.Error);
        }

        await _walletContext.ReloadAsync();
        _logger.LogInformation("Wallet auto-created: {WalletId}", result.Value.Value);
        return Result.Success();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Network Tab Switching
    // ═══════════════════════════════════════════════════════════════════

    public void SelectNetworkTab(NetworkTab tab)
    {
        if (SelectedNetworkTab == tab) return;

        _monitorCts?.Cancel();
        _monitorCts = null;
        ErrorMessage = null;
        IsProcessing = false;
        PaymentReceived = false;
        PaymentStatusText = "Awaiting payment...";

        SelectedNetworkTab = tab;
        _logger.LogInformation("Payment tab switched to {Tab}", tab);

        switch (tab)
        {
            case NetworkTab.OnChain:
                LightningInvoice = null;
                LightningSwapId = null;
                IsProcessing = true;
                PayToOnChainAddress();
                break;
            case NetworkTab.Lightning:
                LightningInvoice = null;
                LightningSwapId = null;
                IsProcessing = true;
                IsGeneratingLightningInvoice = true;
                PaymentStatusText = "Creating Lightning invoice...";
                PayViaLightningCommand.Execute().Subscribe();
                break;
            case NetworkTab.Liquid:
            case NetworkTab.Import:
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // On-Chain Payment Monitoring
    // ═══════════════════════════════════════════════════════════════════

    public void PayToOnChainAddress() => PayToOnChainAddressCommand.Execute().Subscribe(
        onNext: _ => { },
        onError: ex => _logger.LogError(ex, "PayToOnChainAddress subscription error"));

    private async Task PayToOnChainAddressAsync()
    {
        var wallet = Wallets.FirstOrDefault();
        if (wallet?.Id is null || string.IsNullOrEmpty(OnChainAddress))
        {
            ErrorMessage = "Wallet or receive address not ready.";
            return;
        }

        ErrorMessage = null;
        IsProcessing = true;

        _monitorCts?.Cancel();
        var cts = new CancellationTokenSource();
        _monitorCts = cts;

        try
        {
            var walletId = wallet.Id;

            PaymentStatusText = "Waiting for payment...";
            var monitorRequest = new MonitorOp.MonitorAddressForFundsRequest(
                walletId,
                OnChainAddress,
                new Amount(_config.AmountSats),
                TimeSpan.FromMinutes(30));

            var monitorResult = await _investmentAppService.MonitorAddressForFunds(
                monitorRequest, cts.Token);

            if (monitorResult.IsFailure)
            {
                if (cts.IsCancellationRequested)
                {
                    _logger.LogInformation("On-chain monitoring cancelled — suppressing error");
                    return;
                }
                ErrorMessage = monitorResult.Error;
                IsProcessing = false;
                return;
            }

            PaymentStatusText = "Payment received!";
            PaymentReceived = true;

            await RunPostPaymentCallbackAsync(walletId, OnChainAddress);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("On-chain monitoring was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayToOnChainAddressAsync failed");
            ErrorMessage = $"An error occurred: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Lightning Payment (Boltz swap)
    // ═══════════════════════════════════════════════════════════════════

    private async Task PayViaLightningAsync()
    {
        var wallet = Wallets.FirstOrDefault();
        if (wallet?.Id is null || string.IsNullOrEmpty(OnChainAddress))
        {
            ErrorMessage = "Wallet or receive address not ready.";
            IsGeneratingLightningInvoice = false;
            return;
        }

        ErrorMessage = null;
        IsProcessing = true;
        IsGeneratingLightningInvoice = true;

        _monitorCts?.Cancel();
        var cts = new CancellationTokenSource();
        _monitorCts = cts;

        try
        {
            var walletId = wallet.Id;
            var receivingAddress = OnChainAddress;

            // Derive the claim public key from the receive address
            PaymentStatusText = "Preparing Lightning swap...";
            var pubKeyResult = await _walletAppService.GetPublicKeyForAddress(walletId, receivingAddress);
            if (pubKeyResult.IsFailure)
            {
                ErrorMessage = $"Failed to derive claim key: {pubKeyResult.Error}";
                IsProcessing = false;
                IsGeneratingLightningInvoice = false;
                return;
            }

            PaymentStatusText = "Creating Lightning invoice...";
            var swapRequest = new CreateLightningSwap.CreateLightningSwapRequest(
                walletId,
                pubKeyResult.Value,
                new Amount(_config.AmountSats),
                receivingAddress,
                StageCount: _config.StageCount,
                EstimatedFeeRateSatsPerVbyte: _config.FeeRateSatsPerVbyte);

            Result<CreateLightningSwap.CreateLightningSwapResponse> swapResult;
            try
            {
                swapResult = await _investmentAppService.CreateLightningSwap(swapRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateLightningSwap threw");
                ErrorMessage = $"CreateLightningSwap threw: {ex.Message}";
                IsProcessing = false;
                IsGeneratingLightningInvoice = false;
                return;
            }
            if (swapResult.IsFailure)
            {
                _logger.LogError("CreateLightningSwap failed: {Error}", swapResult.Error);
                ErrorMessage = $"CreateLightningSwap failed: {swapResult.Error}";
                IsProcessing = false;
                IsGeneratingLightningInvoice = false;
                return;
            }

            var swap = swapResult.Value.Swap;
            LightningInvoice = swap.Invoice;
            LightningSwapId = swap.Id;
            IsGeneratingLightningInvoice = false;
            _logger.LogInformation("Boltz swap created. SwapId={SwapId}", swap.Id);

            PaymentStatusText = "Waiting for Lightning payment...";
            var monitorSwapRequest = new MonitorLightningSwap.MonitorLightningSwapRequest(
                walletId, swap.Id, TimeSpan.FromMinutes(30));

            var monitorSwapResult = await _investmentAppService.MonitorLightningSwap(monitorSwapRequest);
            if (monitorSwapResult.IsFailure)
            {
                _logger.LogError("MonitorLightningSwap failed: {Error}", monitorSwapResult.Error);
                ErrorMessage = monitorSwapResult.Error;
                IsProcessing = false;
                return;
            }

            PaymentStatusText = "Confirming on-chain claim...";
            var monitorAddressRequest = new MonitorOp.MonitorAddressForFundsRequest(
                walletId, receivingAddress,
                new Amount(_config.AmountSats),
                TimeSpan.FromMinutes(30));

            var monitorAddressResult = await _investmentAppService.MonitorAddressForFunds(
                monitorAddressRequest, cts.Token);
            if (monitorAddressResult.IsFailure)
            {
                if (cts.IsCancellationRequested)
                {
                    _logger.LogInformation("Lightning on-chain monitoring cancelled — suppressing error");
                    return;
                }
                ErrorMessage = monitorAddressResult.Error;
                IsProcessing = false;
                return;
            }

            PaymentStatusText = "Payment received!";
            PaymentReceived = true;

            await RunPostPaymentCallbackAsync(walletId, receivingAddress);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Lightning swap monitoring was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayViaLightningAsync failed");
            ErrorMessage = $"An error occurred: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
            IsGeneratingLightningInvoice = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Post-Payment Callback
    // ═══════════════════════════════════════════════════════════════════

    private async Task RunPostPaymentCallbackAsync(WalletId walletId, string fundingAddress)
    {
        PaymentStatusText = "Processing...";

        var result = await _config.OnPaymentReceived(walletId, fundingAddress, _config.AmountSats);
        if (result.IsFailure)
        {
            ErrorMessage = result.Error;
            return;
        }

        _ = _walletContext.RefreshBalanceAsync(walletId);
        CurrentScreen = PaymentFlowScreen.Success;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Cleanup
    // ═══════════════════════════════════════════════════════════════════

    public void CancelMonitor()
    {
        _monitorCts?.Cancel();
        _monitorCts = null;
    }

    public void OnSuccessButtonClicked() => _config.OnSuccessButtonClicked();

    public void Reset()
    {
        _monitorCts?.Cancel();
        _monitorCts = null;
        CurrentScreen = PaymentFlowScreen.WalletSelector;
        SelectedWallet = null;
        foreach (var w in Wallets) w.IsSelected = false;
        IsProcessing = false;
        PaymentStatusText = "Awaiting payment...";
        PaymentReceived = false;
        ErrorMessage = null;
        SelectedNetworkTab = NetworkTab.OnChain;
        OnChainAddress = null;
        LightningInvoice = null;
        LightningSwapId = null;
        IsGeneratingLightningInvoice = false;
    }
}

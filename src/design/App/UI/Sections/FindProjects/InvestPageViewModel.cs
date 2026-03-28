using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Wallet.Application;
using App.UI.Sections.MyProjects.Deploy;
using App.UI.Sections.Portfolio;
using App.UI.Shared;
using Microsoft.Extensions.Logging;
using MonitorOp = Angor.Sdk.Funding.Investor.Operations.MonitorAddressForFunds;
using ReactiveUI;

namespace App.UI.Sections.FindProjects;

/// <summary>Which screen the invest flow overlay is showing.</summary>
public enum InvestScreen
{
    InvestForm,
    WalletSelector,
    Invoice,
    Success
}

/// <summary>Quick amount option for the investment form.</summary>
public class QuickAmountOption
{
    public double Amount { get; set; }
    public string AmountText { get; set; } = "";
    public string Label { get; set; } = "";
}

/// <summary>Computed stage row for the release schedule column.</summary>
public partial class InvestStageRow : ReactiveObject
{
    [Reactive] private int stageNumber;
    [Reactive] private string releaseDate = "";
    [Reactive] private string percentage = "";
    [Reactive] private string amount = "0.00000000";
    /// <summary>Amount in sats for subscription-type rows (null for investment)</summary>
    [Reactive] private long? amountSats;
    /// <summary>Preformatted label, e.g. "Stage 1" or "Payment 1"</summary>
    [Reactive] private string labelText = "";
    /// <summary>Preformatted amount display, e.g. "0.00100000 BTC" or "50,000 Sats"</summary>
    [Reactive] private string amountDisplayText = "";
    /// <summary>Whether this is a subscription-type row (hides percentage badge)</summary>
    [Reactive] private bool isSubscriptionRow;
}

/// <summary>Subscription plan option (e.g. "3 Months", "6 Months").</summary>
public class SubscriptionPlanOption : ReactiveObject
{
    public string PatternId { get; set; } = "";
    public string Label { get; set; } = "";
    public int Months { get; set; }
    public long TotalSats { get; set; }
    public string PriceText { get; set; } = "";
    public string Description { get; set; } = "";

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }
}

/// <summary>
/// ViewModel for the InvestPage flow.
/// Orchestrates: InvestForm → WalletSelector → Invoice → Success.
/// Connected to SDK for wallet loading and investment operations.
///
/// Vue ref: InvestPage.vue (2984 lines)
/// Supports three project types: Invest, Fund, Subscription.
/// Subscription type shows plan cards (3mo/6mo) instead of BTC amount input.
/// </summary>
public partial class InvestPageViewModel : ReactiveObject
{
    private readonly IWalletAppService _walletAppService;
    private readonly IInvestmentAppService _investmentAppService;
    private readonly PortfolioViewModel _portfolioVm;
    private readonly ICurrencyService _currencyService;
    private readonly ILogger<InvestPageViewModel> _logger;
    private CancellationTokenSource? _invoiceMonitorCts;

    // ── Project Reference ──
    public ProjectItemViewModel Project { get; }

    // ── Type helpers ──
    public bool IsSubscription => Project.ProjectType == "Subscription";
    public bool IsNotSubscription => !IsSubscription;

    // ── Form State ──
    [Reactive] private string investmentAmount = "";
    [Reactive] private double? selectedQuickAmount;
    [Reactive] private InvestScreen currentScreen = InvestScreen.InvestForm;
    [Reactive] private WalletItem? selectedWallet;
    [Reactive] private bool isProcessing;
    [Reactive] private string paymentStatusText = "Awaiting payment...";
    [Reactive] private bool paymentReceived;

    /// <summary>Fee rate in sat/vB, set by FeeSelectionPopup before payment.</summary>
    [Reactive] private long selectedFeeRate = 20;

    // ── Subscription State ──
    [Reactive] private string? selectedSubscriptionPattern;

    // ── Derived visibility ──
    public bool IsInvestForm => CurrentScreen == InvestScreen.InvestForm;
    public bool IsWalletSelector => CurrentScreen == InvestScreen.WalletSelector;
    public bool IsInvoice => CurrentScreen == InvestScreen.Invoice;
    public bool IsSuccess => CurrentScreen == InvestScreen.Success;
    public bool HasSelectedWallet => SelectedWallet != null;
    public string PayButtonText => SelectedWallet != null
        ? $"Pay with {SelectedWallet.Name}"
        : "Choose Wallet";

    // ── Quick Amounts (investment type only) ──
    // Vue ref: quickAmounts grid — 4 items, label is currency symbol
    public ObservableCollection<QuickAmountOption> QuickAmounts { get; } = new();

    /// <summary>Currency symbol from ICurrencyService (e.g. "BTC", "TBTC").</summary>
    public string CurrencySymbol => _currencyService.Symbol;

    /// <summary>e.g. "Enter amount (BTC)"</summary>
    public string AmountLabel => _currencyService.AmountLabel;

    /// <summary>e.g. "Minimum investment: 0.001 BTC"</summary>
    public string MinInvestmentHint => _currencyService.MinInvestmentHint;

    /// <summary>e.g. "1.2345 BTC" — pre-formatted raised amount with currency symbol for the floating pill.</summary>
    public string RaisedWithSymbol => $"{Project.Raised} {_currencyService.Symbol}";

    // ── Subscription Plans (subscription type only) ──
    // Vue ref: subscription-patterns — 2 plan buttons
    public ObservableCollection<SubscriptionPlanOption> SubscriptionPlans { get; } = new();

    // ── Release Schedule / Payment Schedule ──
    public ObservableCollection<InvestStageRow> Stages { get; } = new();

    // ── Transaction Details (stubbed) ──
    public string MinerFee => _currencyService.MinerFeeDisplay;
    public string AngorFee { get; } = Constants.AngorFeeDisplay;
    public string ProjectId => Project.ProjectId;

    // ── Computed totals ──
    public string TotalAmount => ComputeTotal();
    public string FormattedAmount => string.IsNullOrWhiteSpace(InvestmentAmount) ? "0.00000000" : $"{ParseAmount():F8}";
    public string AngorFeeAmount => $"{ParseAmount() * Constants.AngorFeeRate:F8} {_currencyService.Symbol}";

    // Vue ref: subscription shows sats in transaction details
    public string TransactionAmountLabel => IsSubscription ? "Amount to Subscribe" : "Investment Amount";
    public string TransactionAmountValue => IsSubscription
        ? $"{BtcToSats(ParseAmount()):N0} Sats"
        : $"{FormattedAmount} {_currencyService.Symbol}";

    public bool CanSubmit => IsSubscription
        ? SelectedSubscriptionPattern != null && ParseAmount() > 0
        : ParseAmount() >= Constants.MinInvestmentAmount;

    // Vue ref: footer-summary stages/payments count
    private ProjectType TypeEnum => ProjectTypeExtensions.FromDisplayString(Project.ProjectType);

    public string StagesSummary => TypeEnum switch
    {
        ProjectType.Subscription => $"{Stages.Count} payment{(Stages.Count != 1 ? "s" : "")} of {Project.SubscriptionPrice:N0} Sats",
        ProjectType.Fund => $"{Stages.Count} payment{(Stages.Count != 1 ? "s" : "")}",
        _ => $"{Stages.Count} release{(Stages.Count != 1 ? "s" : "")}"
    };

    public string StagesLabel => TypeEnum switch
    {
        ProjectType.Fund or ProjectType.Subscription => "Plan:",
        _ => "Stages:"
    };

    // Vue ref: column 2 header
    public string ScheduleTitle => IsSubscription ? "Payment Schedule" : "Release Schedule";
    public string ScheduleDescription => IsSubscription
        ? "Your subscription payments:"
        : "Funds are released in stages based on project milestones";

    // Vue ref: row label prefix
    public string StageRowPrefix => IsSubscription ? "Payment" : "Stage";

    // ── Success message ──
    public string SuccessTitle => ProjectTypeTerminology.SuccessTitle(TypeEnum);
    public string SuccessDescription => $"Your {Project.ProjectType.ToLower()} of {FormattedAmount} {_currencyService.Symbol} to {Project.ProjectName} has been submitted successfully.";
    public string SuccessButtonText => ProjectTypeTerminology.SuccessButtonText(TypeEnum);

    // ── Wallets loaded from SDK ──
    public ObservableCollection<WalletItem> Wallets { get; } = new();

    public string InvoiceString { get; } = Constants.InvoiceString;

    public InvestPageViewModel(
        ProjectItemViewModel project,
        IWalletAppService walletAppService,
        IInvestmentAppService investmentAppService,
        PortfolioViewModel portfolioVm,
        ICurrencyService currencyService,
        ILogger<InvestPageViewModel> logger)
    {
        Project = project;
        _walletAppService = walletAppService;
        _investmentAppService = investmentAppService;
        _portfolioVm = portfolioVm;
        _currencyService = currencyService;
        _logger = logger;

        _logger.LogInformation("InvestPageViewModel created for project '{ProjectName}' (ID: {ProjectId}, Type: {ProjectType})",
            project.ProjectName, project.ProjectId, project.ProjectType);

        // Initialize quick amounts with dynamic currency label
        var symbol = currencyService.Symbol;
        QuickAmounts.Add(new QuickAmountOption { Amount = 0.001, AmountText = "0.001", Label = symbol });
        QuickAmounts.Add(new QuickAmountOption { Amount = 0.01, AmountText = "0.01", Label = symbol });
        QuickAmounts.Add(new QuickAmountOption { Amount = 0.1, AmountText = "0.1", Label = symbol });
        QuickAmounts.Add(new QuickAmountOption { Amount = 0.5, AmountText = "0.5", Label = symbol });

        // Initialize ReactiveCommands for async payment operations
        PayWithWalletCommand = ReactiveCommand.CreateFromTask(PayWithWalletAsync);
        PayWithWalletCommand.ThrownExceptions.Subscribe(ex => PaymentStatusText = $"Error: {ex.Message}");
        PayViaInvoiceCommand = ReactiveCommand.CreateFromTask(PayViaInvoiceAsync);
        PayViaInvoiceCommand.ThrownExceptions.Subscribe(ex => PaymentStatusText = $"Error: {ex.Message}");

        // Raise derived property notifications when screen changes
        this.WhenAnyValue(x => x.CurrentScreen)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsInvestForm));
                this.RaisePropertyChanged(nameof(IsWalletSelector));
                this.RaisePropertyChanged(nameof(IsInvoice));
                this.RaisePropertyChanged(nameof(IsSuccess));
            });

        this.WhenAnyValue(x => x.SelectedWallet)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(HasSelectedWallet));
                this.RaisePropertyChanged(nameof(PayButtonText));
            });

        // Recompute totals + stages when amount changes
        this.WhenAnyValue(x => x.InvestmentAmount)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(TotalAmount));
                this.RaisePropertyChanged(nameof(FormattedAmount));
                this.RaisePropertyChanged(nameof(AngorFeeAmount));
                this.RaisePropertyChanged(nameof(CanSubmit));
                this.RaisePropertyChanged(nameof(SuccessDescription));
                this.RaisePropertyChanged(nameof(StagesSummary));
                this.RaisePropertyChanged(nameof(TransactionAmountValue));
                RecomputeStages();
            });

        // Recompute when subscription pattern changes
        this.WhenAnyValue(x => x.SelectedSubscriptionPattern)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(CanSubmit));
            });

        // Initialize subscription plans if subscription type
        if (IsSubscription)
        {
            InitializeSubscriptionPlans();
            // Auto-select pattern1
            SelectSubscriptionPlan("pattern1");
        }

        // Initialize stages from project
        RecomputeStages();

        // Load wallets from SDK
        _ = LoadWalletsAsync();
    }

    /// <summary>
    /// Load wallets from SDK for the wallet selector.
    /// </summary>
    private async Task LoadWalletsAsync()
    {
        _logger.LogInformation("Loading wallets for invest flow...");
        try
        {
            var metadatasResult = await _walletAppService.GetMetadatas();
            if (metadatasResult.IsFailure)
            {
                _logger.LogWarning("GetMetadatas failed: {Error}", metadatasResult.Error);
                return;
            }

            Wallets.Clear();
            foreach (var meta in metadatasResult.Value)
            {
                long balanceSats = 0;
                var balanceInfoResult = await _walletAppService.RefreshAndGetAccountBalanceInfo(meta.Id);
                if (balanceInfoResult.IsSuccess)
                {
                    balanceSats = balanceInfoResult.Value.TotalBalance + balanceInfoResult.Value.TotalUnconfirmedBalance;
                }

                var balanceBtc = balanceSats / 100_000_000.0;

                Wallets.Add(new WalletItem
                {
                    Name = meta.Name,
                    Network = "Bitcoin",
                    Balance = $"{balanceBtc:F8} {_currencyService.Symbol}",
                    BalanceSats = balanceSats,
                    WalletId = meta.Id.Value
                });
            }

            _logger.LogInformation("Loaded {Count} wallet(s) for invest flow", Wallets.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load wallets for invest flow");
        }
    }

    // ── Subscription helpers ──

    private static long BtcToSats(double btc) => (long)Math.Round(btc * 100_000_000);
    private static double SatsToBtc(long sats) => sats / 100_000_000.0;

    private long CalculateSubscriptionPrice(string pattern)
    {
        var pricePerMonth = Project.SubscriptionPrice;
        var months = pattern == "pattern1" ? 3 : 6;
        return pricePerMonth * months;
    }

    private void InitializeSubscriptionPlans()
    {
        var price = Project.SubscriptionPrice;
        SubscriptionPlans.Add(new SubscriptionPlanOption
        {
            PatternId = "pattern1",
            Label = "3 Months",
            Months = 3,
            TotalSats = price * 3,
            PriceText = $"{(price * 3):N0} Sats",
            Description = "Monthly payments"
        });
        SubscriptionPlans.Add(new SubscriptionPlanOption
        {
            PatternId = "pattern2",
            Label = "6 Months",
            Months = 6,
            TotalSats = price * 6,
            PriceText = $"{(price * 6):N0} Sats",
            Description = "Monthly payments"
        });
    }

    /// <summary>Select a subscription plan pattern.
    /// Vue ref: clicking a subscription-pattern-btn sets selectedSubscriptionPattern
    /// and computes investmentAmount = satsToBTC(calculateSubscriptionPrice(pattern)).</summary>
    public void SelectSubscriptionPlan(string patternId)
    {
        SelectedSubscriptionPattern = patternId;
        foreach (var plan in SubscriptionPlans)
            plan.IsSelected = plan.PatternId == patternId;

        var totalSats = CalculateSubscriptionPrice(patternId);
        InvestmentAmount = SatsToBtc(totalSats).ToString("F8", System.Globalization.CultureInfo.InvariantCulture);

        this.RaisePropertyChanged(nameof(SubscriptionPlans));
    }

    private double ParseAmount()
    {
        if (double.TryParse(InvestmentAmount, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var val))
            return val;
        return 0;
    }

    private string ComputeTotal()
    {
        var amount = ParseAmount();
        var minerFee = Constants.MinerFee;
        var angorFee = amount * Constants.AngorFeeRate;
        var total = amount + minerFee + angorFee;
        return $"{total:F8} {_currencyService.Symbol}";
    }

    private void RecomputeStages()
    {
        // Subscription: generate payment schedule from selected plan
        if (IsSubscription && SelectedSubscriptionPattern != null)
        {
            var months = SelectedSubscriptionPattern == "pattern1" ? 3 : 6;
            var pricePerMonth = Project.SubscriptionPrice;
            var today = DateTime.Now;

            var newRows = new List<InvestStageRow>(months);
            for (var i = 0; i < months; i++)
            {
                var paymentDate = new DateTime(today.Year, today.Month, 1).AddMonths(i + 1);
                var satsAmount = pricePerMonth;
                newRows.Add(new InvestStageRow
                {
                    StageNumber = i + 1,
                    ReleaseDate = paymentDate.ToString("dd MMM yyyy"),
                    Percentage = $"{(int)Math.Floor(100.0 / months)}%",
                    Amount = SatsToBtc(pricePerMonth).ToString("F8", System.Globalization.CultureInfo.InvariantCulture),
                    AmountSats = satsAmount,
                    LabelText = $"Payment {i + 1}",
                    AmountDisplayText = $"{satsAmount:N0} Sats",
                    IsSubscriptionRow = true
                });
            }

            UpdateStagesInPlace(newRows);
            return;
        }

        // Investment/Fund: use project stages
        var amount = ParseAmount();
        var prefix = StageRowPrefix;
        var newInvestRows = new List<InvestStageRow>();

        if (Project.Stages.Count > 0)
        {
            foreach (var s in Project.Stages)
            {
                var pctStr = s.Percentage.Replace("%", "");
                double.TryParse(pctStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var pct);
                var stageAmount = amount * (pct / 100.0);

                newInvestRows.Add(new InvestStageRow
                {
                    StageNumber = s.StageNumber,
                    ReleaseDate = s.ReleaseDate,
                    Percentage = s.Percentage,
                    Amount = $"{stageAmount:F8}",
                    LabelText = $"{prefix} {s.StageNumber}",
                    AmountDisplayText = $"{stageAmount:F8} {_currencyService.Symbol}",
                    IsSubscriptionRow = false
                });
            }
        }
        else
        {
            for (var i = 1; i <= 4; i++)
            {
                var stageAmount = amount * 0.25;
                newInvestRows.Add(new InvestStageRow
                {
                    StageNumber = i,
                    ReleaseDate = $"Stage {i}",
                    Percentage = "25%",
                    Amount = $"{stageAmount:F8}",
                    LabelText = $"{prefix} {i}",
                    AmountDisplayText = $"{stageAmount:F8} {_currencyService.Symbol}",
                    IsSubscriptionRow = false
                });
            }
        }

        UpdateStagesInPlace(newInvestRows);
    }

    /// <summary>
    /// Update the Stages collection in-place to avoid destroying/recreating ItemsControl children.
    /// Updates existing items' properties, adds new items at end, or removes excess items from end.
    /// This prevents the jarring visual jump that occurs when Clear() + Add() rebuilds all children.
    /// </summary>
    private void UpdateStagesInPlace(List<InvestStageRow> newRows)
    {
        // Update existing rows in-place
        var minCount = Math.Min(Stages.Count, newRows.Count);
        for (var i = 0; i < minCount; i++)
        {
            var existing = Stages[i];
            var updated = newRows[i];
            existing.StageNumber = updated.StageNumber;
            existing.ReleaseDate = updated.ReleaseDate;
            existing.Percentage = updated.Percentage;
            existing.Amount = updated.Amount;
            existing.AmountSats = updated.AmountSats;
            existing.LabelText = updated.LabelText;
            existing.AmountDisplayText = updated.AmountDisplayText;
            existing.IsSubscriptionRow = updated.IsSubscriptionRow;
        }

        // Add new rows if the new list is longer
        for (var i = Stages.Count; i < newRows.Count; i++)
        {
            Stages.Add(newRows[i]);
        }

        // Remove excess rows if the new list is shorter (remove from end to avoid index shifting)
        while (Stages.Count > newRows.Count)
        {
            Stages.RemoveAt(Stages.Count - 1);
        }
    }

    // ── Actions ──

    /// <summary>Select a quick amount and set it as the investment amount.</summary>
    public void SelectQuickAmount(double amount)
    {
        SelectedQuickAmount = amount;
        InvestmentAmount = amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Submit the invest form → show wallet selector.
    /// Vue ref: proceedToPayment() checks balances, opens wallet modal.</summary>
    public void Submit()
    {
        if (!CanSubmit)
        {
            _logger.LogWarning("Submit rejected: CanSubmit is false (amount: {Amount})", InvestmentAmount);
            return;
        }
        _logger.LogInformation("Invest form submitted — amount: {Amount}, advancing to WalletSelector", InvestmentAmount);
        CurrentScreen = InvestScreen.WalletSelector;
    }

    /// <summary>Select a wallet from the list.</summary>
    public void SelectWallet(WalletItem wallet)
    {
        _logger.LogInformation("Wallet selected for investment: '{WalletName}' (ID: {WalletId}, Balance: {Balance})",
            wallet.Name, wallet.WalletId, wallet.Balance);
        foreach (var w in Wallets) w.IsSelected = false;
        wallet.IsSelected = true;
        SelectedWallet = wallet;
    }

    /// <summary>Pay with selected wallet → build draft → check threshold → submit/publish → success.</summary>
    public ReactiveCommand<Unit, Unit> PayWithWalletCommand { get; }

    public void PayWithWallet() => PayWithWalletCommand.Execute().Subscribe();

    private async Task PayWithWalletAsync()
    {
        if (SelectedWallet == null || string.IsNullOrEmpty(SelectedWallet.WalletId))
        {
            PaymentStatusText = "No wallet selected.";
            _logger.LogWarning("PayWithWallet called with no wallet selected");
            return;
        }

        // Balance check: ensure wallet has enough funds for the investment
        var amountBtc = ParseAmount();
        var requiredSats = (long)Math.Round(amountBtc * 100_000_000);
        if (SelectedWallet.BalanceSats < requiredSats)
        {
            var walletBtc = SelectedWallet.BalanceSats / 100_000_000.0;
            PaymentStatusText = $"Insufficient balance. Wallet has {walletBtc:F8} {_currencyService.Symbol}, but {amountBtc:F8} {_currencyService.Symbol} is required.";
            _logger.LogWarning("Insufficient balance: wallet {WalletId} has {BalanceSats} sats, need {RequiredSats} sats",
                SelectedWallet.WalletId, SelectedWallet.BalanceSats, requiredSats);
            return;
        }

        IsProcessing = true;
        PaymentStatusText = "Building investment transaction...";
        _logger.LogInformation("PayWithWallet starting — wallet: {WalletId}, project: {ProjectId}, amount: {Amount} BTC",
            SelectedWallet.WalletId, Project.ProjectId, InvestmentAmount);

        // Yield to let the UI render the spinner before blocking on SDK calls
        await Task.Yield();

        try
        {
            var walletId = new WalletId(SelectedWallet.WalletId);
            var projectId = new ProjectId(Project.ProjectId);
            var amountSats = (long)Math.Round(ParseAmount() * 100_000_000);

            // Determine pattern index for Fund/Subscription projects
            byte? patternIndex = null;
            if (IsSubscription || Project.ProjectType == "Fund")
            {
                patternIndex = SelectedSubscriptionPattern == "pattern2" ? (byte)1 : (byte)0;
            }

            // Build investment draft
            var buildRequest = new BuildInvestmentDraft.BuildInvestmentDraftRequest(
                walletId,
                projectId,
                new Amount(amountSats),
                new DomainFeerate(SelectedFeeRate),
                patternIndex);

            _logger.LogInformation("Building investment draft: wallet={WalletId}, project={ProjectId}, amount={AmountSats} sats, feeRate={FeeRate}, patternIndex={PatternIndex}",
                walletId.Value, projectId.Value, amountSats, SelectedFeeRate, patternIndex);
            var buildResult = await _investmentAppService.BuildInvestmentDraft(buildRequest);
            if (buildResult.IsFailure)
            {
                _logger.LogError("BuildInvestmentDraft failed: {Error}", buildResult.Error);
                PaymentStatusText = "Failed to build transaction.";
                IsProcessing = false;
                return;
            }

            var draft = buildResult.Value.InvestmentDraft;
            _logger.LogInformation("Investment draft built successfully");
            PaymentStatusText = "Checking investment threshold...";

            // Check penalty threshold
            var thresholdRequest = new CheckPenaltyThreshold.CheckPenaltyThresholdRequest(
                projectId,
                new Amount(amountSats));

            var thresholdResult = await _investmentAppService.IsInvestmentAbovePenaltyThreshold(thresholdRequest);

            if (thresholdResult.IsSuccess && thresholdResult.Value.IsAboveThreshold)
            {
                // Above threshold: request founder signatures
                _logger.LogInformation("Investment is above penalty threshold — requesting founder signatures");
                PaymentStatusText = "Requesting founder approval...";
                var submitRequest = new RequestInvestmentSignatures.RequestFounderSignaturesRequest(
                    walletId,
                    projectId,
                    draft);

                var submitResult = await _investmentAppService.SubmitInvestment(submitRequest);
                if (submitResult.IsFailure)
                {
                    _logger.LogError("SubmitInvestment (founder signatures) failed: {Error}", submitResult.Error);
                    PaymentStatusText = "Failed to submit investment.";
                    IsProcessing = false;
                    return;
                }
                _logger.LogInformation("Investment submitted for founder approval");
            }
            else
            {
                // Below threshold: publish directly
                _logger.LogInformation("Investment is below penalty threshold — publishing directly");
                PaymentStatusText = "Publishing transaction...";
                var publishRequest = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
                    walletId.Value,
                    projectId,
                    draft);

                var publishResult = await _investmentAppService.SubmitTransactionFromDraft(publishRequest);
                if (publishResult.IsFailure)
                {
                    _logger.LogError("SubmitTransactionFromDraft failed: {Error}", publishResult.Error);
                    PaymentStatusText = "Failed to publish transaction.";
                    IsProcessing = false;
                    return;
                }
                _logger.LogInformation("Investment transaction published successfully");
            }

            _logger.LogInformation("Investment flow completed — advancing to Success screen");
            CurrentScreen = InvestScreen.Success;
        }
        catch (Exception ex)
        {
            PaymentStatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>Switch to invoice payment screen.
    /// Vue ref: "Or pay an invoice instead" button.</summary>
    public void ShowInvoice()
    {
        CurrentScreen = InvestScreen.Invoice;
    }

    /// <summary>Go back to wallet selector from invoice.</summary>
    public void BackToWalletSelector()
    {
        CurrentScreen = InvestScreen.WalletSelector;
    }

    /// <summary>Simulate paying via invoice → success.
    /// Vue ref: handlePayment() → paymentStatus "received" → success.</summary>
    public ReactiveCommand<Unit, Unit> PayViaInvoiceCommand { get; }

    public void PayViaInvoice() => PayViaInvoiceCommand.Execute().Subscribe();

    private async Task PayViaInvoiceAsync()
    {
        // Use the first available wallet for address generation and monitoring
        var wallet = Wallets.FirstOrDefault();
        if (wallet == null || string.IsNullOrEmpty(wallet.WalletId))
        {
            PaymentStatusText = "No wallet available for invoice monitoring.";
            return;
        }

        IsProcessing = true;
        _invoiceMonitorCts?.Cancel();
        _invoiceMonitorCts = new CancellationTokenSource();

        try
        {
            var walletId = new WalletId(wallet.WalletId);
            var amountSats = (long)Math.Round(ParseAmount() * 100_000_000);

            // Get a receive address to monitor
            PaymentStatusText = "Generating invoice address...";
            var addressResult = await _walletAppService.GetNextReceiveAddress(walletId);
            if (addressResult.IsFailure)
            {
                PaymentStatusText = "Failed to generate receive address.";
                IsProcessing = false;
                return;
            }

            // Monitor the address for incoming funds
            PaymentStatusText = "Waiting for payment...";
            var monitorRequest = new MonitorOp.MonitorAddressForFundsRequest(
                walletId,
                addressResult.Value.Value,
                new Angor.Sdk.Common.Amount(amountSats),
                TimeSpan.FromMinutes(30));

            var monitorResult = await _investmentAppService.MonitorAddressForFunds(
                monitorRequest, _invoiceMonitorCts.Token);

            if (monitorResult.IsFailure)
            {
                PaymentStatusText = "Payment monitoring failed or timed out.";
                IsProcessing = false;
                return;
            }

            PaymentStatusText = "Payment received!";
            PaymentReceived = true;

            // Now proceed with the investment flow (same as PayWithWallet)
            var projectId = new ProjectId(Project.ProjectId);

            PaymentStatusText = "Building investment transaction...";
            var buildRequest = new BuildInvestmentDraft.BuildInvestmentDraftRequest(
                walletId,
                projectId,
                new Amount(amountSats),
                new DomainFeerate(SelectedFeeRate),
                FundingAddress: addressResult.Value.Value);

            var buildResult = await _investmentAppService.BuildInvestmentDraft(buildRequest);
            if (buildResult.IsFailure)
            {
                PaymentStatusText = "Failed to build transaction.";
                IsProcessing = false;
                return;
            }

            var draft = buildResult.Value.InvestmentDraft;

            // Check threshold and submit
            var thresholdRequest = new CheckPenaltyThreshold.CheckPenaltyThresholdRequest(
                projectId,
                new Amount(amountSats));

            var thresholdResult = await _investmentAppService.IsInvestmentAbovePenaltyThreshold(thresholdRequest);

            if (thresholdResult.IsSuccess && thresholdResult.Value.IsAboveThreshold)
            {
                PaymentStatusText = "Requesting founder approval...";
                var submitRequest = new RequestInvestmentSignatures.RequestFounderSignaturesRequest(
                    walletId,
                    projectId,
                    draft);

                var submitResult = await _investmentAppService.SubmitInvestment(submitRequest);
                if (submitResult.IsFailure)
                {
                    PaymentStatusText = "Failed to submit investment.";
                    IsProcessing = false;
                    return;
                }
            }
            else
            {
                PaymentStatusText = "Publishing transaction...";
                var publishRequest = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
                    walletId.Value,
                    projectId,
                    draft);

                var publishResult = await _investmentAppService.SubmitTransactionFromDraft(publishRequest);
                if (publishResult.IsFailure)
                {
                    PaymentStatusText = "Failed to publish transaction.";
                    IsProcessing = false;
                    return;
                }
            }

            CurrentScreen = InvestScreen.Success;
        }
        catch (OperationCanceledException)
        {
            PaymentStatusText = "Invoice monitoring cancelled.";
        }
        catch
        {
            PaymentStatusText = "An error occurred during payment.";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>Close modal overlays, return to invest form.</summary>
    public void CloseModal()
    {
        _invoiceMonitorCts?.Cancel();
        CurrentScreen = InvestScreen.InvestForm;
        SelectedWallet = null;
        foreach (var w in Wallets) w.IsSelected = false;
        IsProcessing = false;
        PaymentStatusText = "Awaiting payment...";
        PaymentReceived = false;
    }

    /// <summary>
    /// Add this investment to the shared Portfolio so it appears in the "Funded" section.
    /// Called after a successful invest flow completes.
    /// </summary>
    public void AddToPortfolio()
    {
        _logger.LogInformation("Adding investment to portfolio: project='{ProjectName}', amount={Amount}",
            Project.ProjectName, FormattedAmount);
        _portfolioVm.AddInvestmentFromProject(Project, FormattedAmount);
    }
}

using System.Collections.ObjectModel;
using System.Threading;
using Angor.Sdk.Common;
using CSharpFunctionalExtensions;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Wallet.Application;
using Angor.Sdk.Wallet.Domain;
using App.UI.Sections.Portfolio;
using Angor.Shared.Models;
using App.UI.Shared;
using ProjectType = App.UI.Shared.ProjectType;
using App.UI.Shared.Services;
using Microsoft.Extensions.Logging;
using MonitorOp = Angor.Sdk.Funding.Investor.Operations.MonitorAddressForFunds;

namespace App.UI.Sections.FindProjects;

/// <summary>Which screen the invest flow overlay is showing.</summary>
public enum InvestScreen
{
    InvestForm,
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

/// <summary>Funding pattern option for Fund-type projects (e.g. "3-Month Monthly", "6-Month Monthly").</summary>
public class FundingPatternOption : ReactiveObject
{
    public byte PatternId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int StageCount { get; set; }
    public string FrequencyText { get; set; } = "";

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
    private readonly IWalletContext _walletContext;
    private readonly Func<BitcoinNetwork> _getNetwork;
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
    [Reactive] private WalletInfo? selectedWallet;
    [Reactive] private bool isProcessing;
    [Reactive] private string paymentStatusText = "Awaiting payment...";
    [Reactive] private bool paymentReceived;

    // ── Invoice-screen tab state ──
    /// <summary>Currently selected payment-network tab in the Invoice screen. Defaults to OnChain.</summary>
    [Reactive] private NetworkTab selectedNetworkTab = NetworkTab.OnChain;

    /// <summary>The on-chain receive address being monitored. Populated when on-chain tab activates.</summary>
    [Reactive] private string? onChainAddress;

    /// <summary>The BOLT11 Lightning invoice from a Boltz submarine swap. Populated when Lightning tab activates.</summary>
    [Reactive] private string? lightningInvoice;

    /// <summary>Boltz swap id for the active Lightning invoice (used by MonitorLightningSwap).</summary>
    [Reactive] private string? lightningSwapId;

    /// <summary>True while CreateLightningSwap is in flight — used to show a spinner over the QR placeholder.</summary>
    [Reactive] private bool isGeneratingLightningInvoice;

    /// <summary>
    /// Error message displayed to the user on the wallet selector modal.
    /// Cleared when processing starts; set when an SDK call fails.
    /// </summary>
    [Reactive] private string? errorMessage;

    /// <summary>Fee rate in sat/vB, set by FeeSelectionPopup before payment.</summary>
    [Reactive] private long selectedFeeRate = 20;

    /// <summary>Whether the investment was auto-approved (published directly, below penalty threshold).
    /// False means it requires founder approval. Used to determine success screen text.</summary>
    [Reactive] private bool isAutoApproved;

    // ── Subscription State ──
    [Reactive] private string? selectedSubscriptionPattern;

    // ── Fund Pattern State ──
    [Reactive] private FundingPatternOption? selectedFundingPattern;

    // ── Derived visibility ──
    public bool IsInvestForm => CurrentScreen == InvestScreen.InvestForm;
    public bool IsWalletSelector => CurrentScreen == InvestScreen.WalletSelector;
    public bool IsInvoice => CurrentScreen == InvestScreen.Invoice;
    public bool IsSuccess => CurrentScreen == InvestScreen.Success;
    public bool HasSelectedWallet => SelectedWallet != null;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public string PayButtonText => SelectedWallet != null
        ? $"Pay with {SelectedWallet.Name}"
        : "Choose Wallet";

    // ── Tab visibility (drives the Selected-class style on each tab Border) ──
    public bool IsOnChainTab => SelectedNetworkTab == NetworkTab.OnChain;
    public bool IsLightningTab => SelectedNetworkTab == NetworkTab.Lightning;
    public bool IsLiquidTab => SelectedNetworkTab == NetworkTab.Liquid;
    public bool IsImportTab => SelectedNetworkTab == NetworkTab.Import;

    /// <summary>Header above the address/invoice text — flips with the active tab.</summary>
    public string InvoiceFieldLabel => SelectedNetworkTab switch
    {
        NetworkTab.Lightning => "Lightning Invoice",
        NetworkTab.Liquid => "Liquid Address",
        NetworkTab.Import => "Imported Invoice",
        _ => "On-Chain Address"
    };

    /// <summary>FontAwesome glyph shown inside the QR placeholder while content loads or as a hint icon.</summary>
    public string InvoiceTabIcon => SelectedNetworkTab switch
    {
        NetworkTab.Lightning => "fa-solid fa-bolt",
        NetworkTab.Liquid => "fa-solid fa-droplet",
        NetworkTab.Import => "fa-solid fa-file-import",
        _ => "fa-brands fa-bitcoin"
    };

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

    // ── Funding Patterns (fund type only) ──
    public ObservableCollection<FundingPatternOption> FundingPatterns { get; } = new();
    public bool ShowFundingPatternSelector => Project.ProjectType == "Fund" && FundingPatterns.Count > 0;
    public bool IsFund => Project.ProjectType == "Fund";

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

    // ── Penalty threshold indicator ──
    /// <summary>Whether the project has a penalty threshold configured.</summary>
    public bool HasPenaltyThreshold => Project.PenaltyThresholdSats.HasValue
                                       || TypeEnum is ProjectType.Fund or ProjectType.Subscription;

    /// <summary>Whether the current investment amount exceeds the penalty threshold (requires founder approval).</summary>
    public bool IsAbovePenaltyThreshold
    {
        get
        {
            if (!Project.PenaltyThresholdSats.HasValue) return true; // no threshold = always requires approval
            var amountSats = (long)((decimal)ParseAmount() * 100_000_000m);
            return amountSats > Project.PenaltyThresholdSats.Value;
        }
    }

    public string ThresholdStatusText => IsAbovePenaltyThreshold ? "Requires Approval" : "No Approval Needed";

    public double SubmitOpacity => CanSubmit ? 1.0 : 0.35;

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
    public string SuccessTitle => ProjectTypeTerminology.SuccessTitle(TypeEnum, IsAutoApproved);
    public string SuccessDescription => ProjectTypeTerminology.SuccessDescription(TypeEnum, IsAutoApproved, FormattedAmount, _currencyService.Symbol, Project.ProjectName);
    public string SuccessButtonText => ProjectTypeTerminology.SuccessButtonText(TypeEnum);

    // ── Wallets loaded from IWalletContext ──
    public ReadOnlyObservableCollection<WalletInfo> Wallets => _walletContext.Wallets;

    /// <summary>
    /// Text shown in the address/invoice field. Tracks the active tab:
    /// on-chain → receive address, lightning → BOLT11 invoice, others → coming-soon placeholder.
    /// While the address/invoice is still being fetched the field falls back to the live PaymentStatusText
    /// so the user sees real progress ("Refreshing wallet...", "Creating Lightning invoice...") rather
    /// than a single static "Generating..." string.
    /// </summary>
    public string InvoiceString => SelectedNetworkTab switch
    {
        NetworkTab.OnChain => OnChainAddress ?? PaymentStatusText,
        NetworkTab.Lightning => LightningInvoice ?? PaymentStatusText,
        _ => Constants.InvoiceString
    };

    /// <summary>
    /// The raw address or invoice for QR code generation — null when not yet available.
    /// Unlike InvoiceString, this does NOT fall back to PaymentStatusText so we don't
    /// accidentally generate a QR code from "Generating invoice address...".
    /// </summary>
    public string? QrCodeContent => SelectedNetworkTab switch
    {
        NetworkTab.OnChain => OnChainAddress,
        NetworkTab.Lightning => LightningInvoice,
        _ => null
    };

    public InvestPageViewModel(
        ProjectItemViewModel project,
        IWalletAppService walletAppService,
        IInvestmentAppService investmentAppService,
        PortfolioViewModel portfolioVm,
        ICurrencyService currencyService,
        IWalletContext walletContext,
        Func<BitcoinNetwork> getNetwork,
        ILogger<InvestPageViewModel> logger)
    {
        Project = project;
        _walletAppService = walletAppService;
        _investmentAppService = investmentAppService;
        _portfolioVm = portfolioVm;
        _currencyService = currencyService;
        _walletContext = walletContext;
        _getNetwork = getNetwork;
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
        PayWithWalletCommand.ThrownExceptions.Subscribe(ex =>
        {
            _logger.LogError(ex, "Unhandled exception in PayWithWalletCommand");
            PaymentStatusText = $"Error: {ex.Message}";
        });
        GenerateReceiveAddressCommand = ReactiveCommand.CreateFromTask(GenerateReceiveAddressAsync);
        GenerateReceiveAddressCommand.ThrownExceptions.Subscribe(ex =>
        {
            _logger.LogError(ex, "Unhandled exception in GenerateReceiveAddressCommand");
            PaymentStatusText = $"Error: {ex.Message}";
        });
        PayToOnChainAddressCommand = ReactiveCommand.CreateFromTask(PayToOnChainAddressAsync);
        PayToOnChainAddressCommand.ThrownExceptions.Subscribe(ex =>
        {
            _logger.LogError(ex, "Unhandled exception in PayToOnChainAddressCommand");
            PaymentStatusText = $"Error: {ex.Message}";
        });
        PayViaLightningCommand = ReactiveCommand.CreateFromTask(PayViaLightningAsync);
        PayViaLightningCommand.ThrownExceptions.Subscribe(ex =>
        {
            _logger.LogError(ex, "Unhandled exception in PayViaLightningCommand");
            PaymentStatusText = $"Error: {ex.Message}";
        });

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

        this.WhenAnyValue(x => x.ErrorMessage)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(HasError)));

        // Tab switch → re-emit derived visibility, label, icon, and the invoice text binding.
        this.WhenAnyValue(x => x.SelectedNetworkTab)
            .Subscribe(_ =>
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

        // Address/invoice payload changes → refresh the bound text and QR code.
        this.WhenAnyValue(x => x.OnChainAddress, x => x.LightningInvoice, x => x.IsGeneratingLightningInvoice)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(InvoiceString));
                this.RaisePropertyChanged(nameof(QrCodeContent));
            });

        // Live progress text feeds the placeholder shown before the address/invoice is ready.
        this.WhenAnyValue(x => x.PaymentStatusText)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(InvoiceString)));

        // Update success messages when auto-approval status is determined
        this.WhenAnyValue(x => x.IsAutoApproved)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(SuccessTitle));
                this.RaisePropertyChanged(nameof(SuccessDescription));
            });

        // Recompute totals + stages when amount changes
        this.WhenAnyValue(x => x.InvestmentAmount)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(TotalAmount));
                this.RaisePropertyChanged(nameof(FormattedAmount));
                this.RaisePropertyChanged(nameof(AngorFeeAmount));
                this.RaisePropertyChanged(nameof(CanSubmit));
                this.RaisePropertyChanged(nameof(SubmitOpacity));
                this.RaisePropertyChanged(nameof(SuccessDescription));
                this.RaisePropertyChanged(nameof(StagesSummary));
                this.RaisePropertyChanged(nameof(TransactionAmountValue));
                this.RaisePropertyChanged(nameof(IsAbovePenaltyThreshold));
                this.RaisePropertyChanged(nameof(ThresholdStatusText));
                RecomputeStages();
            });

        // Recompute when subscription pattern changes
        this.WhenAnyValue(x => x.SelectedSubscriptionPattern)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(CanSubmit));
                this.RaisePropertyChanged(nameof(SubmitOpacity));
            });

        // Initialize subscription plans if subscription type.
        // No auto-select: CanSubmit stays false until the user picks a plan,
        // matching the Fund/Invest path (button dimmed until amount entered).
        if (IsSubscription)
        {
            InitializeSubscriptionPlans();
        }

        // Initialize funding patterns if fund type
        if (IsFund)
        {
            InitializeFundingPatterns();
        }

        // Recompute when funding pattern changes
        this.WhenAnyValue(x => x.SelectedFundingPattern)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(StagesSummary));
                RecomputeStages();
            });

        // Initialize stages from project
        RecomputeStages();
    }

    // ── Subscription helpers ──

    private static long BtcToSats(double btc) => ((decimal)btc).ToUnitSatoshi();
    private static double SatsToBtc(long sats) => (double)sats.ToUnitBtc();

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

    // ── Fund Pattern helpers ──

    private void InitializeFundingPatterns()
    {
        foreach (var pattern in Project.DynamicStagePatterns)
        {
            FundingPatterns.Add(new FundingPatternOption
            {
                PatternId = pattern.PatternId,
                Name = pattern.Name ?? $"Pattern {pattern.PatternId}",
                Description = pattern.DisplayDescription,
                StageCount = pattern.StageCount,
                FrequencyText = pattern.Frequency.ToString()
            });
        }

        if (FundingPatterns.Count > 0)
        {
            SelectFundingPattern(FundingPatterns[0]);
        }

        this.RaisePropertyChanged(nameof(ShowFundingPatternSelector));
    }

    /// <summary>Select a funding pattern for Fund-type projects.</summary>
    public void SelectFundingPattern(FundingPatternOption option)
    {
        SelectedFundingPattern = option;
        foreach (var p in FundingPatterns)
            p.IsSelected = p.PatternId == option.PatternId;

        this.RaisePropertyChanged(nameof(FundingPatterns));
    }

    /// <summary>
    /// Generate synthetic stages from a DynamicStagePattern (Fund-type projects).
    /// Mirrors the Avalonia reference: GenerateStagesFromPattern in InvestViewModel.cs.
    /// </summary>
    private List<InvestStageRow> GenerateStagesFromFundingPattern(DynamicStagePattern pattern, double amount)
    {
        var stageCount = pattern.StageCount;
        if (stageCount <= 0)
            return new List<InvestStageRow>();

        var ratioPerStage = 1.0 / stageCount;
        var now = DateTimeOffset.UtcNow;
        var rows = new List<InvestStageRow>(stageCount);

        for (var i = 0; i < stageCount; i++)
        {
            var releaseDate = ComputePatternReleaseDate(now, pattern, i + 1);
            var stageAmount = amount * ratioPerStage;
            var pctStr = $"{ratioPerStage * 100:F0}%";
            rows.Add(new InvestStageRow
            {
                StageNumber = i + 1,
                ReleaseDate = releaseDate.ToString("dd MMM yyyy"),
                Percentage = pctStr,
                Amount = $"{stageAmount:F8}",
                LabelText = $"Stage {i + 1}",
                AmountDisplayText = $"{stageAmount:F8} {_currencyService.Symbol}",
                IsSubscriptionRow = false
            });
        }

        return rows;
    }

    private static DateTimeOffset ComputePatternReleaseDate(DateTimeOffset startDate, DynamicStagePattern pattern, int stageNumber)
    {
        return pattern.PayoutDayType switch
        {
            PayoutDayType.FromStartDate => AddFrequencyIntervals(startDate, pattern.Frequency, stageNumber),
            PayoutDayType.SpecificDayOfMonth => ComputeSpecificDayOfMonth(startDate, pattern, stageNumber),
            PayoutDayType.SpecificDayOfWeek => ComputeSpecificDayOfWeek(startDate, pattern, stageNumber),
            _ => AddFrequencyIntervals(startDate, pattern.Frequency, stageNumber)
        };
    }

    private static DateTimeOffset AddFrequencyIntervals(DateTimeOffset startDate, StageFrequency frequency, int intervals)
    {
        return frequency switch
        {
            StageFrequency.Weekly => startDate.AddDays(7 * intervals),
            StageFrequency.Biweekly => startDate.AddDays(14 * intervals),
            StageFrequency.Monthly => startDate.AddMonths(intervals),
            StageFrequency.BiMonthly => startDate.AddMonths(2 * intervals),
            StageFrequency.Quarterly => startDate.AddMonths(3 * intervals),
            _ => startDate.AddMonths(intervals)
        };
    }

    private static DateTimeOffset ComputeSpecificDayOfMonth(DateTimeOffset startDate, DynamicStagePattern pattern, int stageNumber)
    {
        var monthsToAdd = pattern.Frequency switch
        {
            StageFrequency.Monthly => stageNumber,
            StageFrequency.BiMonthly => 2 * stageNumber,
            StageFrequency.Quarterly => 3 * stageNumber,
            _ => stageNumber
        };

        var target = startDate.AddMonths(monthsToAdd);
        var day = Math.Min(pattern.PayoutDay, DateTime.DaysInMonth(target.Year, target.Month));
        return new DateTimeOffset(target.Year, target.Month, day, 0, 0, 0, target.Offset);
    }

    private static DateTimeOffset ComputeSpecificDayOfWeek(DateTimeOffset startDate, DynamicStagePattern pattern, int stageNumber)
    {
        var weeksToAdd = pattern.Frequency switch
        {
            StageFrequency.Weekly => stageNumber,
            StageFrequency.Biweekly => 2 * stageNumber,
            _ => stageNumber
        };

        var target = startDate.AddDays(7 * weeksToAdd);
        var currentDay = (int)target.DayOfWeek;
        var targetDay = pattern.PayoutDay;
        var diff = targetDay - currentDay;
        return target.AddDays(diff);
    }

    /// <summary>
    /// Auto-create a wallet if none exists. Used by the 1-click invest flow so the user
    /// doesn't need to visit the Funds section before investing.
    /// </summary>
    private async Task<Result> EnsureWalletExistsAsync()
    {
        _logger.LogInformation("No wallet found — auto-creating for 1-click invest flow");
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
            var today = DateTime.UtcNow;

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

        // Fund type with a selected dynamic pattern: generate synthetic stages
        if (IsFund && SelectedFundingPattern != null)
        {
            var pattern = Project.DynamicStagePatterns
                .FirstOrDefault(p => p.PatternId == SelectedFundingPattern.PatternId);
            if (pattern != null)
            {
                newInvestRows = GenerateStagesFromFundingPattern(pattern, amount);
                UpdateStagesInPlace(newInvestRows);
                return;
            }
        }

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
    public void SelectWallet(WalletInfo wallet)
    {
        _logger.LogInformation("Wallet selected for investment: '{WalletName}' (ID: {WalletId}, Balance: {Balance})",
            wallet.Name, wallet.Id.Value, wallet.Balance);
        foreach (var w in Wallets) w.IsSelected = false;
        wallet.IsSelected = true;
        SelectedWallet = wallet;
    }

    /// <summary>Pay with selected wallet → build draft → check threshold → submit/publish → success.</summary>
    public ReactiveCommand<Unit, Unit> PayWithWalletCommand { get; }

    public void PayWithWallet() => PayWithWalletCommand.Execute().Subscribe(
        onNext: _ => { },
        onError: ex => _logger.LogError(ex, "PayWithWallet subscription error"));

    private async Task PayWithWalletAsync()
    {
        if (SelectedWallet == null)
        {
            ErrorMessage = "No wallet selected.";
            _logger.LogWarning("PayWithWallet called with no wallet selected");
            return;
        }

        ErrorMessage = null;
        IsProcessing = true;

        // Refresh wallet UTXOs from the indexer before building the transaction.
        // This ensures we don't pick UTXOs already consumed by a prior transaction
        // (e.g. a project deploy) that haven't been synced to local LiteDB yet.
        PaymentStatusText = "Refreshing wallet...";
        _logger.LogInformation("Refreshing wallet UTXOs before building investment draft...");
        await _walletContext.RefreshAllBalancesAsync();

        // Balance check: ensure wallet has enough funds for the investment
        var amountBtc = ParseAmount();
        var requiredSats = ((decimal)amountBtc).ToUnitSatoshi();
        if (SelectedWallet.AvailableSats < requiredSats)
        {
            var walletBtc = SelectedWallet.AvailableSats.ToUnitBtc();
            ErrorMessage = $"Insufficient balance. Wallet has {walletBtc:F8} {_currencyService.Symbol}, but {amountBtc:F8} {_currencyService.Symbol} is required.";
            _logger.LogWarning("Insufficient balance: wallet {WalletId} has {BalanceSats} sats, need {RequiredSats} sats",
                SelectedWallet.Id.Value, SelectedWallet.AvailableSats, requiredSats);
            IsProcessing = false;
            return;
        }

        PaymentStatusText = "Building investment transaction...";
        _logger.LogInformation("PayWithWallet starting — wallet: {WalletId}, project: {ProjectId}, amount: {Amount} BTC",
            SelectedWallet.Id.Value, Project.ProjectId, InvestmentAmount);

        // Yield to let the UI render the spinner before blocking on SDK calls
        await Task.Yield();

        try
        {
            var walletId = SelectedWallet.Id;
            var projectId = new ProjectId(Project.ProjectId);
            var amountSats = ((decimal)ParseAmount()).ToUnitSatoshi();

            // Determine pattern index for Fund/Subscription projects
            byte? patternIndex = null;
            if (IsSubscription)
            {
                patternIndex = SelectedSubscriptionPattern == "pattern2" ? (byte)1 : (byte)0;
            }
            else if (IsFund && SelectedFundingPattern != null)
            {
                patternIndex = SelectedFundingPattern.PatternId;
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
                ErrorMessage = buildResult.Error;
                IsProcessing = false;
                return;
            }

            var draft = buildResult.Value.InvestmentDraft;
            _logger.LogInformation("Investment draft built successfully");

            // Investment-type projects always require founder approval (no threshold check).
            // Fund-type projects require approval only when the amount exceeds the penalty threshold.
            var isAboveThreshold = Project.ProjectType == "Invest";
            if (Project.ProjectType == "Fund")
            {
                PaymentStatusText = "Checking investment threshold...";

                var thresholdRequest = new CheckPenaltyThreshold.CheckPenaltyThresholdRequest(
                    projectId,
                    new Amount(amountSats));

                var thresholdResult = await _investmentAppService.IsInvestmentAbovePenaltyThreshold(thresholdRequest);
                if (thresholdResult.IsFailure)
                {
                    _logger.LogError("CheckPenaltyThreshold failed: {Error}", thresholdResult.Error);
                    ErrorMessage = thresholdResult.Error;
                    IsProcessing = false;
                    return;
                }

                isAboveThreshold = thresholdResult.Value.IsAboveThreshold;
            }

            if (isAboveThreshold)
            {
                // Above threshold or investment-type: request founder signatures
                IsAutoApproved = false;
                _logger.LogInformation("Investment requires founder approval — requesting founder signatures");
                PaymentStatusText = "Requesting founder approval...";
                var submitRequest = new RequestInvestmentSignatures.RequestFounderSignaturesRequest(
                    walletId,
                    projectId,
                    draft);

                var submitResult = await _investmentAppService.SubmitInvestment(submitRequest);
                if (submitResult.IsFailure)
                {
                    _logger.LogError("SubmitInvestment (founder signatures) failed: {Error}", submitResult.Error);
                    ErrorMessage = submitResult.Error;
                    IsProcessing = false;
                    return;
                }
                _logger.LogInformation("Investment submitted for founder approval");
            }
            else
            {
                // Below threshold: publish directly
                IsAutoApproved = true;
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
                    ErrorMessage = publishResult.Error;
                    IsProcessing = false;
                    return;
                }
                _logger.LogInformation("Investment transaction published successfully");
            }

            _logger.LogInformation("Investment flow completed — advancing to Success screen");
            _ = _walletContext.RefreshBalanceAsync(walletId).ContinueWith(t =>
            {
                if (t.IsFaulted) _logger.LogWarning(t.Exception, "Background RefreshBalanceAsync failed for wallet {WalletId}", walletId.Value);
            }, TaskContinuationOptions.OnlyOnFaulted);
            CurrentScreen = InvestScreen.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayWithWalletAsync failed");
            PaymentStatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>Switch to invoice payment screen. Generates the receive address once
    /// (wallet creation + refresh + address generation), then starts on-chain monitoring.
    /// Both on-chain and Lightning tabs reuse this address.
    /// Vue ref: "Or pay an invoice instead" button.</summary>
    public void ShowInvoice()
    {
        CurrentScreen = InvestScreen.Invoice;
        SelectedNetworkTab = NetworkTab.OnChain;
        LightningInvoice = null;
        LightningSwapId = null;
        OnChainAddress = null;
        ErrorMessage = null;
        IsProcessing = true;
        PaymentStatusText = "Generating invoice address...";
        GenerateReceiveAddressCommand.Execute().Subscribe();
    }

    /// <summary>Generates the receive address (creating a wallet if needed), then starts on-chain monitoring.</summary>
    public ReactiveCommand<Unit, Unit> GenerateReceiveAddressCommand { get; }

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
            ErrorMessage = "Wallet has no ID — wallet store may be in an inconsistent state.";
            IsProcessing = false;
            return;
        }
        if (string.IsNullOrEmpty(Project?.ProjectId))
        {
            ErrorMessage = "Project has no ID.";
            IsProcessing = false;
            return;
        }

        PaymentStatusText = "Refreshing wallet...";
        _logger.LogInformation("Refreshing wallet UTXOs...");
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
            _logger.LogError(ex, "GetNextReceiveAddress threw for wallet {WalletId}", wallet.Id.Value);
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

        // Start on-chain monitoring (default tab)
        PayToOnChainAddress();
    }

    /// <summary>Go back to wallet selector from invoice.</summary>
    public void BackToWalletSelector()
    {
        CurrentScreen = InvestScreen.WalletSelector;
    }

    /// <summary>
    /// Switch the active payment-network tab inside the Invoice modal.
    /// Cancels any running monitoring loop before kicking off the new tab's flow.
    /// Lightning click triggers Boltz swap creation + monitoring + claim → publish.
    /// On-chain click re-enters the address-monitoring loop.
    /// Liquid/Import are visual stubs for now.
    /// </summary>
    public void SelectNetworkTab(NetworkTab tab)
    {
        if (SelectedNetworkTab == tab) return;

        // Cancel whatever monitoring/swap loop is running before switching paths.
        _invoiceMonitorCts?.Cancel();
        _invoiceMonitorCts = null;
        ErrorMessage = null;
        IsProcessing = false;
        PaymentReceived = false;
        PaymentStatusText = "Awaiting payment...";

        SelectedNetworkTab = tab;
        _logger.LogInformation("Invoice tab switched to {Tab}", tab);

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

    /// <summary>Generate an on-chain receive address, monitor it for incoming funds,
    /// and on detection run the build → threshold → publish pipeline.
    /// Vue ref: handlePayment() → paymentStatus "received" → success.</summary>
    public ReactiveCommand<Unit, Unit> PayToOnChainAddressCommand { get; }

    public void PayToOnChainAddress() => PayToOnChainAddressCommand.Execute().Subscribe(
        onNext: _ => { },
        onError: ex => _logger.LogError(ex, "PayToOnChainAddress subscription error"));

    private async Task PayToOnChainAddressAsync()
    {
        var wallet = Wallets.FirstOrDefault();
        if (wallet?.Id is null || string.IsNullOrEmpty(wallet.Id.Value) ||
            string.IsNullOrEmpty(OnChainAddress))
        {
            ErrorMessage = "Wallet or receive address not ready.";
            return;
        }

        ErrorMessage = null;
        IsProcessing = true;

        _invoiceMonitorCts?.Cancel();
        var cts = new CancellationTokenSource();
        _invoiceMonitorCts = cts;

        try
        {
            var walletId = wallet.Id;
            var amountSats = ((decimal)ParseAmount()).ToUnitSatoshi();

            PaymentStatusText = "Waiting for payment...";
            var monitorRequest = new MonitorOp.MonitorAddressForFundsRequest(
                walletId,
                OnChainAddress,
                new Angor.Sdk.Common.Amount(amountSats),
                TimeSpan.FromMinutes(30));

            var monitorResult = await _investmentAppService.MonitorAddressForFunds(
                monitorRequest, cts.Token);

            if (monitorResult.IsFailure)
            {
                if (cts.IsCancellationRequested)
                {
                    _logger.LogInformation("On-chain monitoring returned failure after cancellation — suppressing error");
                    return;
                }
                ErrorMessage = monitorResult.Error;
                IsProcessing = false;
                return;
            }

            PaymentStatusText = "Payment received!";
            PaymentReceived = true;

            await CompleteInvestmentAfterFundingAsync(walletId, OnChainAddress, amountSats);
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

    /// <summary>
    /// Lightning ("1-click invest") path. Creates a Boltz submarine swap so the user can pay a BOLT11 invoice
    /// from any Lightning wallet — Boltz then locks on-chain funds at a HTLC address that we claim with our
    /// preimage. Once the claim transaction lands, the funds appear at our normal receive address and we run
    /// the same build → threshold → publish tail used by the on-chain path.
    /// </summary>
    public ReactiveCommand<Unit, Unit> PayViaLightningCommand { get; }

    public void PayViaLightning() => PayViaLightningCommand.Execute().Subscribe();

    private async Task PayViaLightningAsync()
    {
        var wallet = Wallets.FirstOrDefault();
        if (wallet?.Id is null || string.IsNullOrEmpty(wallet.Id.Value) ||
            string.IsNullOrEmpty(OnChainAddress) || string.IsNullOrEmpty(Project?.ProjectId))
        {
            ErrorMessage = "Wallet or receive address not ready.";
            IsGeneratingLightningInvoice = false;
            return;
        }

        ErrorMessage = null;
        IsProcessing = true;
        IsGeneratingLightningInvoice = true;

        _invoiceMonitorCts?.Cancel();
        var cts = new CancellationTokenSource();
        _invoiceMonitorCts = cts;

        try
        {
            var walletId = wallet.Id;
            var receivingAddress = OnChainAddress;
            var projectId = new ProjectId(Project.ProjectId);
            var amountSats = ((decimal)ParseAmount()).ToUnitSatoshi();

            PaymentStatusText = "Creating Lightning invoice...";
            _logger.LogInformation("Creating Boltz Lightning swap: wallet={WalletId}, project={ProjectId}, amount={AmountSats} sats",
                walletId.Value, projectId.Value, amountSats);
            var swapRequest = new CreateLightningSwapForInvestment.CreateLightningSwapRequest(
                walletId,
                projectId,
                new Amount(amountSats),
                receivingAddress,
                StageCount: Stages.Count,
                EstimatedFeeRateSatsPerVbyte: (int)SelectedFeeRate);

            Result<CreateLightningSwapForInvestment.CreateLightningSwapResponse> swapResult;
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
                walletId,
                swap.Id,
                TimeSpan.FromMinutes(30));

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
                walletId,
                receivingAddress,
                new Angor.Sdk.Common.Amount(amountSats),
                TimeSpan.FromMinutes(30));

            var monitorAddressResult = await _investmentAppService.MonitorAddressForFunds(
                monitorAddressRequest, cts.Token);
            if (monitorAddressResult.IsFailure)
            {
                if (cts.IsCancellationRequested)
                {
                    _logger.LogInformation("Lightning on-chain monitoring returned failure after cancellation — suppressing error");
                    return;
                }
                ErrorMessage = monitorAddressResult.Error;
                IsProcessing = false;
                return;
            }

            PaymentStatusText = "Payment received!";
            PaymentReceived = true;

            await CompleteInvestmentAfterFundingAsync(walletId, receivingAddress, amountSats);
        }
        catch (OperationCanceledException)
        {
            // Tab switch or modal close — not a user-facing error.
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

    /// <summary>
    /// Shared tail of both invoice paths (on-chain + Lightning): given a wallet that has just received funds
    /// at <paramref name="fundingAddress"/>, build the investment draft, decide whether founder approval is
    /// required, then either request signatures or publish directly. Caller owns ErrorMessage/IsProcessing.
    /// </summary>
    private async Task CompleteInvestmentAfterFundingAsync(WalletId walletId, string fundingAddress, long amountSats)
    {
        var projectId = new ProjectId(Project.ProjectId);

        // Fund/Subscription projects require a pattern index (same logic as PayWithWalletAsync).
        byte? patternIndex = null;
        if (IsSubscription)
        {
            patternIndex = SelectedSubscriptionPattern == "pattern2" ? (byte)1 : (byte)0;
        }
        else if (IsFund && SelectedFundingPattern != null)
        {
            patternIndex = SelectedFundingPattern.PatternId;
        }

        PaymentStatusText = "Building investment transaction...";
        var buildRequest = new BuildInvestmentDraft.BuildInvestmentDraftRequest(
            walletId,
            projectId,
            new Amount(amountSats),
            new DomainFeerate(SelectedFeeRate),
            PatternId: patternIndex,
            FundingAddress: fundingAddress);

        var buildResult = await _investmentAppService.BuildInvestmentDraft(buildRequest);
        if (buildResult.IsFailure)
        {
            ErrorMessage = buildResult.Error;
            return;
        }

        var draft = buildResult.Value.InvestmentDraft;

        // Investment-type projects always require founder approval (no threshold check).
        // Fund-type projects require approval only when the amount exceeds the penalty threshold.
        var isAboveThreshold = Project.ProjectType == "Invest";
        if (Project.ProjectType == "Fund")
        {
            PaymentStatusText = "Checking investment threshold...";
            var thresholdRequest = new CheckPenaltyThreshold.CheckPenaltyThresholdRequest(
                projectId,
                new Amount(amountSats));

            var thresholdResult = await _investmentAppService.IsInvestmentAbovePenaltyThreshold(thresholdRequest);
            if (thresholdResult.IsFailure)
            {
                ErrorMessage = thresholdResult.Error;
                return;
            }
            isAboveThreshold = thresholdResult.Value.IsAboveThreshold;
        }

        if (isAboveThreshold)
        {
            IsAutoApproved = false;
            PaymentStatusText = "Requesting founder approval...";
            var submitRequest = new RequestInvestmentSignatures.RequestFounderSignaturesRequest(
                walletId,
                projectId,
                draft);

            var submitResult = await _investmentAppService.SubmitInvestment(submitRequest);
            if (submitResult.IsFailure)
            {
                ErrorMessage = submitResult.Error;
                return;
            }
        }
        else
        {
            IsAutoApproved = true;
            PaymentStatusText = "Publishing transaction...";
            var publishRequest = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
                walletId.Value,
                projectId,
                draft);

            var publishResult = await _investmentAppService.SubmitTransactionFromDraft(publishRequest);
            if (publishResult.IsFailure)
            {
                ErrorMessage = publishResult.Error;
                return;
            }
        }

        _ = _walletContext.RefreshBalanceAsync(walletId);
        CurrentScreen = InvestScreen.Success;
    }

    /// <summary>Cancel the active invoice/address monitoring loop (if any).
    /// Used by tests to let the on-chain path settle before switching tabs.</summary>
    public void CancelInvoiceMonitor()
    {
        _invoiceMonitorCts?.Cancel();
        _invoiceMonitorCts = null;
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
        ErrorMessage = null;
        SelectedNetworkTab = NetworkTab.OnChain;
        OnChainAddress = null;
        LightningInvoice = null;
        LightningSwapId = null;
        IsGeneratingLightningInvoice = false;
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

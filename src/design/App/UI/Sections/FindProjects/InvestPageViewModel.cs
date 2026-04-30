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
using Angor.Shared.Integration.Lightning;
using App.UI.Shared;
using App.UI.Shared.PaymentFlow;
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
    private readonly IBoltzSwapService _boltzSwapService;
    private readonly PortfolioViewModel _portfolioVm;
    private readonly ICurrencyService _currencyService;
    private readonly IWalletContext _walletContext;
    private readonly Func<BitcoinNetwork> _getNetwork;
    private readonly ILogger<InvestPageViewModel> _logger;
    // ── Project Reference ──
    public ProjectItemViewModel Project { get; }

    /// <summary>The reusable payment flow VM. Created when the user advances past the invest form.</summary>
    [Reactive] private PaymentFlowViewModel? paymentFlow;

    // ── Type helpers ──
    public bool IsSubscription => Project.ProjectType == "Subscription";
    public bool IsNotSubscription => !IsSubscription;

    // ── Form State ──
    [Reactive] private string investmentAmount = "";
    [Reactive] private double? selectedQuickAmount;
    [Reactive] private InvestScreen currentScreen = InvestScreen.InvestForm;

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

    public double SubmitOpacity => CanSubmit ? 1.0 : 0.45;

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

    // ── Wallets loaded from IWalletContext ──
    public ReadOnlyObservableCollection<WalletInfo> Wallets => _walletContext.Wallets;

    public InvestPageViewModel(
        ProjectItemViewModel project,
        IWalletAppService walletAppService,
        IInvestmentAppService investmentAppService,
        IBoltzSwapService boltzSwapService,
        PortfolioViewModel portfolioVm,
        ICurrencyService currencyService,
        IWalletContext walletContext,
        Func<BitcoinNetwork> getNetwork,
        ILogger<InvestPageViewModel> logger)
    {
        Project = project;
        _walletAppService = walletAppService;
        _investmentAppService = investmentAppService;
        _boltzSwapService = boltzSwapService;
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

        // Raise derived property notifications when screen changes
        this.WhenAnyValue(x => x.CurrentScreen)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsInvestForm));
                this.RaisePropertyChanged(nameof(IsWalletSelector));
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

    /// <summary>Submit the invest form → create payment flow and show wallet selector.
    /// Vue ref: proceedToPayment() checks balances, opens wallet modal.</summary>
    public void Submit()
    {
        if (!CanSubmit)
        {
            _logger.LogWarning("Submit rejected: CanSubmit is false (amount: {Amount})", InvestmentAmount);
            return;
        }
        _logger.LogInformation("Invest form submitted — amount: {Amount}, advancing to WalletSelector", InvestmentAmount);

        var amountSats = ((decimal)ParseAmount()).ToUnitSatoshi();
        PaymentFlow = CreatePaymentFlow(amountSats);
        CurrentScreen = InvestScreen.WalletSelector;
    }

    /// <summary>Creates a PaymentFlowViewModel configured for the current investment.</summary>
    private PaymentFlowViewModel CreatePaymentFlow(long amountSats)
    {
        var projectType = Project.ProjectType;
        var typeEnum = ProjectTypeExtensions.FromDisplayString(projectType);
        var actionVerb = ProjectTypeTerminology.ActionVerb(typeEnum);
        var successTitle = ProjectTypeTerminology.SuccessTitle(typeEnum, true);

        var config = new PaymentFlowConfig
        {
            AmountSats = amountSats,
            StageCount = Stages.Count,
            FeeRateSatsPerVbyte = 20,
            Title = $"Pay to {actionVerb}",
            SuccessTitle = successTitle,
            SuccessDescription = $"Your {actionVerb.ToLowerInvariant()} of {FormattedAmount} {_currencyService.Symbol} has been submitted.",
            SuccessButtonText = $"View My {ProjectTypeTerminology.InvestorNounTotal(ProjectTypeExtensions.FromDisplayString(projectType))}",
            OnSuccessButtonClicked = OnInvestSuccess,
            OnPaymentReceived = async (walletId, fundingAddress, amount) =>
                await InvestAfterPaymentAsync(walletId, fundingAddress, amount),
            OnPayWithWallet = async (walletId, amount, feeRate) =>
                await InvestWithWalletAsync(walletId, amount, feeRate),
        };

        return new PaymentFlowViewModel(
            _walletAppService,
            _investmentAppService,
            _boltzSwapService,
            _walletContext,
            _currencyService,
            _getNetwork,
            _logger,
            config);
    }

    /// <summary>Callback for PaymentFlowViewModel: build + publish investment after external payment.</summary>
    private async Task<Result> InvestAfterPaymentAsync(WalletId walletId, string fundingAddress, long amountSats)
    {
        var projectId = new ProjectId(Project.ProjectId);
        byte? patternIndex = GetPatternIndex();

        var buildRequest = new BuildInvestmentDraft.BuildInvestmentDraftRequest(
            walletId, projectId, new Amount(amountSats),
            new DomainFeerate(PaymentFlow?.SelectedFeeRate ?? 20),
            PatternId: patternIndex,
            FundingAddress: fundingAddress);

        var buildResult = await _investmentAppService.BuildInvestmentDraft(buildRequest);
        if (buildResult.IsFailure)
            return Result.Failure(buildResult.Error);

        return await PublishOrRequestApprovalAsync(walletId, projectId, amountSats, buildResult.Value.InvestmentDraft);
    }

    /// <summary>Callback for PaymentFlowViewModel: build + publish investment from wallet UTXOs.</summary>
    private async Task<Result> InvestWithWalletAsync(WalletId walletId, long amountSats, long feeRate)
    {
        await _walletContext.RefreshAllBalancesAsync();

        var projectId = new ProjectId(Project.ProjectId);
        byte? patternIndex = GetPatternIndex();

        var buildRequest = new BuildInvestmentDraft.BuildInvestmentDraftRequest(
            walletId, projectId, new Amount(amountSats),
            new DomainFeerate(feeRate),
            patternIndex);

        var buildResult = await _investmentAppService.BuildInvestmentDraft(buildRequest);
        if (buildResult.IsFailure)
            return Result.Failure(buildResult.Error);

        return await PublishOrRequestApprovalAsync(walletId, projectId, amountSats, buildResult.Value.InvestmentDraft);
    }

    /// <summary>Shared logic: threshold check → request signatures or publish directly.</summary>
    private async Task<Result> PublishOrRequestApprovalAsync(
        WalletId walletId, ProjectId projectId, long amountSats,
        Angor.Sdk.Funding.Shared.TransactionDrafts.InvestmentDraft draft)
    {
        var isAboveThreshold = Project.ProjectType == "Invest";
        if (Project.ProjectType == "Fund")
        {
            var thresholdResult = await _investmentAppService.IsInvestmentAbovePenaltyThreshold(
                new CheckPenaltyThreshold.CheckPenaltyThresholdRequest(projectId, new Amount(amountSats)));
            if (thresholdResult.IsFailure)
                return Result.Failure(thresholdResult.Error);
            isAboveThreshold = thresholdResult.Value.IsAboveThreshold;
        }

        if (isAboveThreshold)
        {
            IsAutoApproved = false;
            var submitResult = await _investmentAppService.SubmitInvestment(
                new RequestInvestmentSignatures.RequestFounderSignaturesRequest(walletId, projectId, draft));
            if (submitResult.IsFailure)
                return Result.Failure(submitResult.Error);
        }
        else
        {
            IsAutoApproved = true;
            var publishResult = await _investmentAppService.SubmitTransactionFromDraft(
                new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
                    walletId.Value, projectId, draft));
            if (publishResult.IsFailure)
                return Result.Failure(publishResult.Error);
        }

        // Update the success screen text now that we know if it was auto-approved
        var typeEnum = ProjectTypeExtensions.FromDisplayString(Project.ProjectType);
        if (PaymentFlow != null)
        {
            PaymentFlow.SuccessTitle = ProjectTypeTerminology.SuccessTitle(typeEnum, IsAutoApproved);
            PaymentFlow.SuccessDescription = ProjectTypeTerminology.SuccessDescription(
                typeEnum, IsAutoApproved, FormattedAmount, _currencyService.Symbol, Project.ProjectName);
        }

        return Result.Success();
    }

    private byte? GetPatternIndex()
    {
        if (IsSubscription)
            return SelectedSubscriptionPattern == "pattern2" ? (byte)1 : (byte)0;
        if (IsFund && SelectedFundingPattern != null)
            return SelectedFundingPattern.PatternId;
        return null;
    }

    /// <summary>Raised when the invest flow completes and the user clicks the success button.
    /// The view subscribes to navigate to the Funded section.</summary>
    public event Action? InvestCompleted;

    private void OnInvestSuccess()
    {
        AddToPortfolio();
        InvestCompleted?.Invoke();
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

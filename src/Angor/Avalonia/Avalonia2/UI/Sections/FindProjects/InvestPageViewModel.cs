using System.Collections.ObjectModel;
using System.Reactive;
using Avalonia2.UI.Sections.MyProjects.Deploy;
using Avalonia2.UI.Sections.Portfolio;
using Avalonia2.UI.Shared;
using ReactiveUI;

namespace Avalonia2.UI.Sections.FindProjects;

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
/// All data is stubbed — no SDK dependencies.
///
/// Vue ref: InvestPage.vue (2984 lines)
/// Supports three project types: Invest, Fund, Subscription.
/// Subscription type shows plan cards (3mo/6mo) instead of BTC amount input.
/// </summary>
public partial class InvestPageViewModel : ReactiveObject
{
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
    // Vue ref: quickAmounts grid — 4 items, label is "BTC"
    public ObservableCollection<QuickAmountOption> QuickAmounts { get; } = new()
    {
        new() { Amount = 0.001, AmountText = "0.001", Label = "BTC" },
        new() { Amount = 0.01, AmountText = "0.01", Label = "BTC" },
        new() { Amount = 0.1, AmountText = "0.1", Label = "BTC" },
        new() { Amount = 0.5, AmountText = "0.5", Label = "BTC" }
    };

    // ── Subscription Plans (subscription type only) ──
    // Vue ref: subscription-patterns — 2 plan buttons
    public ObservableCollection<SubscriptionPlanOption> SubscriptionPlans { get; } = new();

    // ── Release Schedule / Payment Schedule ──
    public ObservableCollection<InvestStageRow> Stages { get; } = new();

    // ── Transaction Details (stubbed) ──
    public string MinerFee { get; } = Constants.MinerFeeDisplay;
    public string AngorFee { get; } = Constants.AngorFeeDisplay;
    public string ProjectId => Project.ProjectId;

    // ── Computed totals ──
    public string TotalAmount => ComputeTotal();
    public string FormattedAmount => string.IsNullOrWhiteSpace(InvestmentAmount) ? "0.00000000" : $"{ParseAmount():F8}";
    public string AngorFeeAmount => $"{ParseAmount() * Constants.AngorFeeRate:F8} BTC";

    // Vue ref: subscription shows sats in transaction details
    public string TransactionAmountLabel => IsSubscription ? "Amount to Subscribe" : "Investment Amount";
    public string TransactionAmountValue => IsSubscription
        ? $"{BtcToSats(ParseAmount()):N0} Sats"
        : $"{FormattedAmount} BTC";

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
    public string SuccessDescription => $"Your {Project.ProjectType.ToLower()} of {FormattedAmount} BTC to {Project.ProjectName} has been submitted successfully.";
    public string SuccessButtonText => ProjectTypeTerminology.SuccessButtonText(TypeEnum);

    // ── Stub wallets (reuse from DeployFlowOverlay) ──
    public ObservableCollection<WalletItem> Wallets { get; } = new()
    {
        new() { Name = "Main Wallet", Network = "Mainnet", Balance = "0.04821000 BTC" },
        new() { Name = "Angor Wallet", Network = "Signet", Balance = "1.25000000 BTC" },
        new() { Name = "Test Wallet", Network = "Testnet", Balance = "0.50000000 BTC" }
    };

    public string InvoiceString { get; } = Constants.InvoiceString;

    public InvestPageViewModel(ProjectItemViewModel project)
    {
        Project = project;

        // Initialize ReactiveCommands for async payment operations
        PayWithWalletCommand = ReactiveCommand.CreateFromTask(PayWithWalletAsync);
        PayViaInvoiceCommand = ReactiveCommand.CreateFromTask(PayViaInvoiceAsync);

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
        return $"{total:F8} BTC";
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
                    AmountDisplayText = $"{stageAmount:F8} BTC",
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
                    AmountDisplayText = $"{stageAmount:F8} BTC",
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
        if (!CanSubmit) return;
        CurrentScreen = InvestScreen.WalletSelector;
    }

    /// <summary>Select a wallet from the list.</summary>
    public void SelectWallet(WalletItem wallet)
    {
        foreach (var w in Wallets) w.IsSelected = false;
        wallet.IsSelected = true;
        SelectedWallet = wallet;
    }

    /// <summary>Pay with selected wallet → simulate processing → success.
    /// Vue ref: payWithWallet() → 800ms spinner → "received" → 1500ms → success.</summary>
    public ReactiveCommand<Unit, Unit> PayWithWalletCommand { get; }

    public void PayWithWallet() => PayWithWalletCommand.Execute().Subscribe();

    private async Task PayWithWalletAsync()
    {
        if (SelectedWallet == null) return;
        IsProcessing = true;
        PaymentStatusText = "Processing payment...";
        await Task.Delay(2500);
        CurrentScreen = InvestScreen.Success;
        IsProcessing = false;
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
        IsProcessing = true;
        PaymentStatusText = "Payment received!";
        PaymentReceived = true;
        await Task.Delay(2000);
        CurrentScreen = InvestScreen.Success;
        IsProcessing = false;
    }

    /// <summary>Close modal overlays, return to invest form.</summary>
    public void CloseModal()
    {
        CurrentScreen = InvestScreen.InvestForm;
        SelectedWallet = null;
        foreach (var w in Wallets) w.IsSelected = false;
        IsProcessing = false;
        PaymentStatusText = "Awaiting payment...";
        PaymentReceived = false;
    }
}

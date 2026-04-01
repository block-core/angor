using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Wallet.Application;
using Avalonia.Media.Imaging;
using App.UI.Sections.FindProjects;
using App.UI.Shared;
using App.UI.Shared.Helpers;
using App.UI.Shared.Services;
using App.UI.Shell;
using Microsoft.Extensions.Logging;

namespace App.UI.Sections.Portfolio;

/// <summary>
/// Recovery state model matching the Avalonia reference implementation.
/// Uses all 5 boolean fields from the SDK's InvestorProjectRecoveryDto.
/// </summary>
public record RecoveryState(
    bool HasUnspentItems,
    bool HasSpendableItemsInPenalty,
    bool HasReleaseSignatures,
    bool EndOfProject,
    bool IsAboveThreshold)
{
    public static readonly RecoveryState None = new(false, false, false, false, false);

    /// <summary>
    /// Determines which recovery action is available, matching the Avalonia reference priority order.
    /// </summary>
    public string ButtonLabel => this switch
    {
        { HasUnspentItems: true, HasReleaseSignatures: true } => "Recover without Penalty",
        { HasUnspentItems: true, EndOfProject: true } or { HasUnspentItems: true, IsAboveThreshold: false } => "Recover",
        { HasUnspentItems: true, HasSpendableItemsInPenalty: false } => "Recover to Penalty",
        { HasSpendableItemsInPenalty: true } => "Recover from Penalty",
        _ => string.Empty
    };

    /// <summary>
    /// Maps to a recovery action key used by the modal routing.
    /// </summary>
    public string ActionKey => this switch
    {
        { HasUnspentItems: true, HasReleaseSignatures: true } => "unfundedRelease",
        { HasUnspentItems: true, EndOfProject: true } or { HasUnspentItems: true, IsAboveThreshold: false } => "endOfProject",
        { HasUnspentItems: true, HasSpendableItemsInPenalty: false } => "recovery",
        { HasSpendableItemsInPenalty: true } => "penaltyRelease",
        _ => "none"
    };

    public bool HasAction => !string.IsNullOrEmpty(ButtonLabel);
}

/// <summary>
/// A stage in an investment's release schedule.
/// </summary>
public class InvestmentStageViewModel
{
    public int StageNumber { get; set; }
    public string Percentage { get; set; } = "0%";
    public string ReleaseDate { get; set; } = "";
    public string Amount { get; set; } = "0.00000000";
    public string Status { get; set; } = "Pending"; // Pending, Released, Available, Not Spent
    /// <summary>Stage label prefix: "Stage" for invest, "Payment" for fund/subscription</summary>
    public string StagePrefix { get; set; } = "Stage";

    // Status visibility helpers for per-status badge coloring
    public bool IsStatusPending => Status == "Pending";
    public bool IsStatusReleased => Status == "Released";
    public bool IsStatusNotSpent => Status == "Not Spent";
    public bool IsStatusRecovered => Status == "Recovered";
}

/// <summary>
/// A funded project investment shown in the "Your Investments" panel.
/// Data from Vue reference — Hope With Bitcoin investment.
/// Vue: investment-card in App.vue (desktop Investments page) and HubInvestments.vue
/// Implements INotifyPropertyChanged so Step/Status changes propagate to the UI
/// (e.g. when a founder approves a signature in the Funders section).
/// </summary>
public class InvestmentViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public string ProjectName { get; set; } = "";
    public string ShortDescription { get; set; } = "";
    public string TotalInvested { get; set; } = "0.00000000";
    public string AvailableToClaim { get; set; } = "0.00000000";
    public string Spent { get; set; } = "0.00000000";
    public double Progress { get; set; }
    /// <summary>Progress as a fraction 0.0–1.0 for ScaleTransform binding</summary>
    public double ProgressFraction => Progress / 100.0;
    public string Status { get; set; } = "Active";
    /// <summary>Funding amount displayed on the investment card</summary>
    public string FundingAmount { get; set; } = "0.0000";
    /// <summary>Date string displayed on the investment card</summary>
    public string FundingDate { get; set; } = "";
    /// <summary>Number of completed payment segments</summary>
    public int PaymentSegmentsCompleted { get; set; }
    /// <summary>Total number of payment segments</summary>
    public int PaymentSegmentsTotal { get; set; } = 3;
    /// <summary>Whether segment 1 is completed (for green fill in UI)</summary>
    public bool IsSegment1Complete => PaymentSegmentsCompleted >= 1;
    /// <summary>Whether segment 2 is completed</summary>
    public bool IsSegment2Complete => PaymentSegmentsCompleted >= 2;
    /// <summary>Whether segment 3 is completed</summary>
    public bool IsSegment3Complete => PaymentSegmentsCompleted >= 3;
    /// <summary>Banner image URL</summary>
    private string? _bannerUrl;
    public string? BannerUrl
    {
        get => _bannerUrl;
        set
        {
            _bannerUrl = value;
            ImageCacheService.LoadBitmapAsync(value, bmp => { BannerBitmap = bmp; OnPropertyChanged(nameof(BannerBitmap)); });
        }
    }
    /// <summary>Avatar/logo image URL</summary>
    private string? _avatarUrl;
    public string? AvatarUrl
    {
        get => _avatarUrl;
        set
        {
            _avatarUrl = value;
            ImageCacheService.LoadBitmapAsync(value, bmp => { AvatarBitmap = bmp; OnPropertyChanged(nameof(AvatarBitmap)); });
        }
    }
    /// <summary>Decoded banner bitmap, loaded from <see cref="BannerUrl"/> via ImageCacheService.</summary>
    public Bitmap? BannerBitmap { get; private set; }
    /// <summary>Decoded avatar bitmap, loaded from <see cref="AvatarUrl"/> via ImageCacheService.</summary>
    public Bitmap? AvatarBitmap { get; private set; }

    // ── Type and Status pills (Vue: .investment-pills, .investment-type-pill, .stage-status) ──
    /// <summary>Type pill label: Investment, Funding, Subscription</summary>
    public string TypeLabel { get; set; } = "Investment";

    // ── Mutable properties that raise change notifications ──
    // These are updated by OnSignatureStatusChanged when a founder approves/rejects.

    private string _statusText = "Transaction signed";
    /// <summary>Status pill text: Awaiting Approval, Transaction signed, Investment Active, Funds recovered</summary>
    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText == value) return;
            _statusText = value;
            OnPropertyChanged();
        }
    }

    private string _statusClass = "signed";
    /// <summary>Status class: waiting, signed, active, recovered, rejected</summary>
    public string StatusClass
    {
        get => _statusClass;
        set
        {
            if (_statusClass == value) return;
            _statusClass = value;
            OnPropertyChanged();
            // Raise dependent computed properties
            OnPropertyChanged(nameof(IsStatusWaiting));
            OnPropertyChanged(nameof(IsStatusSigned));
            OnPropertyChanged(nameof(IsStatusActive));
            OnPropertyChanged(nameof(IsStatusRecovered));
        }
    }

    // Status visibility helpers for XAML binding
    public bool IsStatusWaiting => StatusClass == "waiting";
    public bool IsStatusSigned => StatusClass == "signed";
    public bool IsStatusActive => StatusClass == "active";
    public bool IsStatusRecovered => StatusClass == "recovered";

    // Type visibility helpers for XAML binding (invest=blue, fund=amber, subscription=purple)
    public bool IsTypeInvest => ProjectType == "invest";
    public bool IsTypeFund => ProjectType == "fund";
    public bool IsTypeSubscription => ProjectType == "subscription";

    /// <summary>Whether this is a subscription/fund type with payment plan progress</summary>
    public bool HasPaymentPlan { get; set; }

    // Legacy pill properties — kept for InvestmentDetailView compatibility
    public string StatusPill1 { get; set; } = "Funding";

    private string _statusPill2 = "Transaction signed";
    public string StatusPill2
    {
        get => _statusPill2;
        set
        {
            if (_statusPill2 == value) return;
            _statusPill2 = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Project type: invest, fund, subscription</summary>
    public string ProjectType { get; set; } = "invest";

    private int _step = 3;
    /// <summary>Investment step: 1=waiting, 2=preview, 3=active</summary>
    public int Step
    {
        get => _step;
        set
        {
            if (_step == value) return;
            _step = value;
            OnPropertyChanged();
            // Raise dependent computed properties used in InvestmentDetailView step pills
            OnPropertyChanged(nameof(IsStep1));
            OnPropertyChanged(nameof(IsStep2));
            OnPropertyChanged(nameof(IsStep3));
            OnPropertyChanged(nameof(IsStepAtLeast2));
            OnPropertyChanged(nameof(IsStepAtLeast3));
        }
    }

    /// <summary>Target/Goal amount for the project</summary>
    public string TargetAmount { get; set; } = "0.0000";
    /// <summary>Total raised across all investors</summary>
    public string TotalRaised { get; set; } = "0.0000";
    /// <summary>Total investor count</summary>
    public int TotalInvestors { get; set; }
    /// <summary>Currency symbol for display (e.g. "BTC", "TBTC")</summary>
    public string CurrencySymbol { get; set; } = "BTC";
    /// <summary>Start date of the investment</summary>
    public string StartDate { get; set; } = "";
    /// <summary>End date of the investment</summary>
    public string EndDate { get; set; } = "";
    /// <summary>Transaction date</summary>
    public string TransactionDate { get; set; } = "";

    private string _approvalStatus = "Approved";
    /// <summary>Approval status text</summary>
    public string ApprovalStatus
    {
        get => _approvalStatus;
        set
        {
            if (_approvalStatus == value) return;
            _approvalStatus = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Cross-reference to SharedSignature.Id for approval flow</summary>
    public int SignatureId { get; set; }
    /// <summary>SDK project identifier for operations</summary>
    public string ProjectIdentifier { get; set; } = "";
    /// <summary>SDK wallet ID used for this investment</summary>
    public string InvestmentWalletId { get; set; } = "";
    /// <summary>Transaction ID/hash of the investment</summary>
    public string InvestmentTransactionId { get; set; } = "";
    public ObservableCollection<InvestmentStageViewModel> Stages { get; set; } = new();

    // ── Recovery State (replaces simplified string-based PenaltyState) ──
    // Uses all 5 boolean fields from SDK's InvestorProjectRecoveryDto

    private RecoveryState _recoveryState = RecoveryState.None;
    /// <summary>Full recovery state with all 5 SDK fields</summary>
    public RecoveryState RecoveryState
    {
        get => _recoveryState;
        set
        {
            if (_recoveryState == value) return;
            _recoveryState = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowRecoverButton));
            OnPropertyChanged(nameof(PenaltyButtonText));
            OnPropertyChanged(nameof(PenaltyButtonIcon));
            OnPropertyChanged(nameof(RecoveryActionKey));
        }
    }

    /// <summary>Whether the recovery button should be shown</summary>
    public bool ShowRecoverButton => ApprovalStatus == "Approved" && Step == 3 && RecoveryState.HasAction;

    /// <summary>Dynamic button text per recovery state (matches Avalonia reference)</summary>
    public string PenaltyButtonText => RecoveryState.ButtonLabel;

    /// <summary>Action key for modal routing</summary>
    public string RecoveryActionKey => RecoveryState.ActionKey;

    /// <summary>Dynamic button icon per recovery state</summary>
    public string PenaltyButtonIcon => RecoveryState.ActionKey switch
    {
        "none" => "fa-solid fa-arrows-rotate",
        _ => "fa-solid fa-circle-check"
    };

    /// <summary>Number of unreleased stages (Vue: stagesToRecover computed)</summary>
    public int StagesToRecover => Stages.Count(s => s.Status != "Released");

    /// <summary>Sum of unreleased stage amounts (Vue: amountToRecover computed)</summary>
    public string AmountToRecover
    {
        get
        {
            var total = Stages.Where(s => s.Status != "Released")
                .Sum(s => double.TryParse(s.Amount, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0);
            return total.ToString("F5", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    // ── Recovery Modal Visibility State ──

    private bool _showRecoveryModal;
    public bool ShowRecoveryModal
    {
        get => _showRecoveryModal;
        set { if (_showRecoveryModal == value) return; _showRecoveryModal = value; OnPropertyChanged(); }
    }

    private bool _showClaimModal;
    public bool ShowClaimModal
    {
        get => _showClaimModal;
        set { if (_showClaimModal == value) return; _showClaimModal = value; OnPropertyChanged(); }
    }

    private bool _showReleaseModal;
    public bool ShowReleaseModal
    {
        get => _showReleaseModal;
        set { if (_showReleaseModal == value) return; _showReleaseModal = value; OnPropertyChanged(); }
    }

    private bool _showSuccessModal;
    public bool ShowSuccessModal
    {
        get => _showSuccessModal;
        set { if (_showSuccessModal == value) return; _showSuccessModal = value; OnPropertyChanged(); }
    }

    private string _selectedFeePriority = "standard";
    /// <summary>Fee priority: "priority", "standard", "economy"</summary>
    public string SelectedFeePriority
    {
        get => _selectedFeePriority;
        set
        {
            if (_selectedFeePriority == value) return;
            _selectedFeePriority = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsFeePriority));
            OnPropertyChanged(nameof(IsFeeStandard));
            OnPropertyChanged(nameof(IsFeeEconomy));
        }
    }

    public bool IsFeePriority => SelectedFeePriority == "priority";
    public bool IsFeeStandard => SelectedFeePriority == "standard";
    public bool IsFeeEconomy => SelectedFeePriority == "economy";

    private bool _isCustomFeeRate;
    public bool IsCustomFeeRate
    {
        get => _isCustomFeeRate;
        set { if (_isCustomFeeRate == value) return; _isCustomFeeRate = value; OnPropertyChanged(); }
    }

    private bool _isProcessing;
    public bool IsProcessing
    {
        get => _isProcessing;
        set { if (_isProcessing == value) return; _isProcessing = value; OnPropertyChanged(); }
    }

    // ── Recovery data (populated from SDK when available) ──
    public string PenaltyDuration { get; set; } = "";
    public string MinerFee { get; set; } = "";
    public string DestinationAddress { get; set; } = "";
    public string RecoveryProjectId => ProjectIdentifier;
    public string PenaltyAmount => AmountToRecover;
    public int PenaltyDaysRemaining { get; set; }

    // ── Step visibility helpers (for XAML binding without converters) ──
    public bool IsStep1 => Step == 1;
    public bool IsStep2 => Step == 2;
    public bool IsStep3 => Step == 3;
    public bool IsStepAtLeast2 => Step >= 2;
    public bool IsStepAtLeast3 => Step >= 3;

    // ── Type-specific terminology (via shared ProjectTypeTerminology) ──
    private Shared.ProjectType TypeEnum => ProjectTypeExtensions.FromLowerString(ProjectType);

    /// <summary>Action verb: "Invest" / "Fund" / "Subscribe"</summary>
    public string ActionVerb => ProjectTypeTerminology.ActionVerb(TypeEnum);

    /// <summary>Amount noun: "Investment" / "Funding" / "Subscription"</summary>
    public string AmountNoun => ProjectTypeTerminology.AmountNoun(TypeEnum);

    /// <summary>Stage label: "Stage" / "Payment"</summary>
    public string StageLabel => ProjectTypeTerminology.StageLabel(TypeEnum);

    /// <summary>Schedule title: "Release Schedule" / "Payment Schedule"</summary>
    public string ScheduleTitle => ProjectTypeTerminology.ScheduleTitle(TypeEnum);

    /// <summary>Progress label: "Funding Progress" / "Subscription Progress"</summary>
    public string ProgressLabel => ProjectType switch
    {
        "subscription" => "Subscription Progress",
        _ => "Funding Progress"
    };

    /// <summary>Investor noun: "Investors" / "Funders" / "Subscribers"</summary>
    public string InvestorNoun => ProjectTypeTerminology.InvestorNounPlural(TypeEnum);

    /// <summary>Target label: "Target Amount" / "Goal Amount"</summary>
    public string TargetLabel => ProjectTypeTerminology.TargetNoun(TypeEnum);

    /// <summary>Total raised label: "Total Raised" / "Total Funded" / "Total Subscribed"</summary>
    public string TotalRaisedLabel => ProjectTypeTerminology.RaisedNoun(TypeEnum);

    /// <summary>Your amount label: "Your Investment" / "Your Funding" / "Your Subscription"</summary>
    public string YourAmountLabel => ProjectType switch
    {
        "fund" => "Your Funding",
        "subscription" => "Your Subscription",
        _ => "Your Investment"
    };
}

/// <summary>
/// Portfolio/Funded ViewModel — connected to SDK for investment discovery and management.
/// Uses IInvestmentAppService for loading investments and performing recovery/release operations.
/// </summary>
public partial class PortfolioViewModel : ReactiveObject
{
    private readonly IInvestmentAppService _investmentAppService;
    private readonly IWalletContext _walletContext;
    private readonly SignatureStore _signatureStore;
    private readonly ICurrencyService _currencyService;
    private readonly ILogger<PortfolioViewModel> _logger;

    public event Action<string>? ToastRequested;

    [Reactive] private bool hasInvestments;
    [Reactive] private InvestmentViewModel? selectedInvestment;
    [Reactive] private bool isLoading;

    // ── Left panel stats ──
    public int FundedProjects { get; private set; }
    public string TotalInvested { get; private set; } = "0.0000";
    public string RecoveredToPenalty { get; private set; } = "0.0000";
    public int ProjectsInRecovery { get; private set; }
    public string TotalAvailable { get; private set; } = "0.0000";

    // ── Right panel investments ──
    public ObservableCollection<InvestmentViewModel> Investments { get; } = new();

    /// <summary>Currency symbol from ICurrencyService (e.g. "BTC", "TBTC").</summary>
    public string CurrencySymbol => _currencyService.Symbol;

    public PortfolioViewModel(
        IInvestmentAppService investmentAppService,
        IWalletContext walletContext,
        SignatureStore signatureStore,
        ICurrencyService currencyService,
        ILogger<PortfolioViewModel> logger)
    {
        _investmentAppService = investmentAppService;
        _walletContext = walletContext;
        _signatureStore = signatureStore;
        _currencyService = currencyService;
        _logger = logger;

        _logger.LogInformation("PortfolioViewModel created");

        // Listen for signature status changes to update investment steps
        _signatureStore.SignatureStatusChanged += OnSignatureStatusChanged;

        // Load investments from SDK
        _ = LoadInvestmentsFromSdkAsync();
    }

    /// <summary>
    /// Load investments from SDK for all wallets.
    /// </summary>
    public async Task LoadInvestmentsFromSdkAsync()
    {
        IsLoading = true;
        _logger.LogInformation("Loading investments from SDK...");

        try
        {
            var wallets = _walletContext.Wallets;
            if (wallets.Count == 0)
            {
                _logger.LogInformation("No wallets found — clearing investments");
                ClearToEmpty();
                return;
            }

            Investments.Clear();
            double totalInvested = 0;
            double totalInRecovery = 0;
            int recoveryCount = 0;

            foreach (var wallet in wallets)
            {
                var investmentsResult = await _investmentAppService.GetInvestments(
                    new GetInvestments.GetInvestmentsRequest(wallet.Id));

                if (investmentsResult.IsFailure) continue;

                foreach (var dto in investmentsResult.Value.Projects)
                {
                    var investedBtc = (double)dto.Investment.Sats.ToUnitBtc();
                    var raisedBtc = (double)dto.Raised.Sats.ToUnitBtc();
                    var targetBtc = (double)dto.Target.Sats.ToUnitBtc();
                    var inRecoveryBtc = (double)dto.InRecovery.Sats.ToUnitBtc();

                    totalInvested += investedBtc;
                    if (dto.InRecovery.Sats > 0)
                    {
                        totalInRecovery += inRecoveryBtc;
                        recoveryCount++;
                    }

                    var (statusText, statusClass, step) = MapInvestmentStatus(dto.InvestmentStatus, dto.FounderStatus);

                    var existingInvestment = Investments.FirstOrDefault(i =>
                        (!string.IsNullOrEmpty(dto.Id) && i.ProjectIdentifier == dto.Id) ||
                        (!string.IsNullOrEmpty(dto.InvestmentId) && i.InvestmentTransactionId == dto.InvestmentId));

                    var serverStatus = dto.InvestmentStatus;
                    if (existingInvestment != null)
                    {
                        var localStatus = MapUiStepToInvestmentStatus(existingInvestment);
                        if (localStatus != serverStatus && IsStaleResponse(localStatus, serverStatus))
                        {
                            _logger.LogInformation(
                                "Keeping optimistic local status {LocalStatus} for project {ProjectId}; server still reports {ServerStatus}",
                                localStatus,
                                dto.Id,
                                serverStatus);

                            statusText = existingInvestment.StatusText;
                            statusClass = existingInvestment.StatusClass;
                            step = existingInvestment.Step;
                        }
                    }

                    var vm = new InvestmentViewModel
                    {
                        ProjectName = dto.Name ?? "Unknown Project",
                        ShortDescription = dto.Description ?? "",
                        TotalInvested = investedBtc.ToString("F8", CultureInfo.InvariantCulture),
                        FundingAmount = $"{investedBtc:F4} {_currencyService.Symbol}",
                        FundingDate = DateTime.Now.ToString("M/dd/yyyy"),
                        TypeLabel = "Investment",
                        StatusText = statusText,
                        StatusClass = statusClass,
                        StatusPill1 = "Funding",
                        StatusPill2 = statusText,
                        TargetAmount = targetBtc.ToString("F4", CultureInfo.InvariantCulture),
                        TotalRaised = raisedBtc.ToString("F4", CultureInfo.InvariantCulture),
                        Progress = targetBtc > 0 ? Math.Min(100, raisedBtc / targetBtc * 100) : 0,
                        Status = statusClass == "active" ? "Active" : "Pending",
                        ProjectType = "invest",
                        Step = step,
                        ApprovalStatus = dto.FounderStatus == Angor.Sdk.Funding.Investor.FounderStatus.Approved ? "Approved" : "Pending",
                        AvatarUrl = dto.LogoUri?.ToString(),
                        ProjectIdentifier = dto.Id ?? "",
                        InvestmentWalletId = wallet.Id.Value,
                        InvestmentTransactionId = dto.InvestmentId ?? "",
                        CurrencySymbol = _currencyService.Symbol
                    };

                    Investments.Add(vm);
                }
            }

            FundedProjects = Investments.Count;
            TotalInvested = totalInvested.ToString("F4", CultureInfo.InvariantCulture);
            RecoveredToPenalty = totalInRecovery.ToString("F4", CultureInfo.InvariantCulture);
            ProjectsInRecovery = recoveryCount;
            HasInvestments = Investments.Count > 0;

            _logger.LogInformation("Investments loaded: {Count} investment(s), totalInvested={TotalInvested} BTC, inRecovery={RecoveryCount}",
                Investments.Count, TotalInvested, recoveryCount);

            this.RaisePropertyChanged(nameof(FundedProjects));
            this.RaisePropertyChanged(nameof(TotalInvested));
            this.RaisePropertyChanged(nameof(RecoveredToPenalty));
            this.RaisePropertyChanged(nameof(ProjectsInRecovery));
            this.RaisePropertyChanged(nameof(TotalAvailable));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading investments from SDK");
            ClearToEmpty();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Map SDK InvestmentStatus/FounderStatus to UI status text, class, and step.
    /// </summary>
    private static (string statusText, string statusClass, int step) MapInvestmentStatus(
        Angor.Sdk.Funding.Founder.InvestmentStatus investmentStatus,
        Angor.Sdk.Funding.Investor.FounderStatus founderStatus)
    {
        return investmentStatus switch
        {
            Angor.Sdk.Funding.Founder.InvestmentStatus.PendingFounderSignatures =>
                ("Awaiting Approval", "waiting", 1),
            Angor.Sdk.Funding.Founder.InvestmentStatus.FounderSignaturesReceived =>
                ("Transaction signed", "signed", 2),
            Angor.Sdk.Funding.Founder.InvestmentStatus.Invested =>
                ("Investment Active", "active", 3),
            Angor.Sdk.Funding.Founder.InvestmentStatus.Cancelled =>
                ("Cancelled", "recovered", 3),
            _ => ("Unknown", "waiting", 1)
        };
    }

    private static InvestmentStatus MapUiStepToInvestmentStatus(InvestmentViewModel investment)
    {
        if (investment.Status == "Cancelled" || investment.StatusText == "Cancelled")
            return InvestmentStatus.Cancelled;

        return investment.Step switch
        {
            3 => InvestmentStatus.Invested,
            2 => InvestmentStatus.FounderSignaturesReceived,
            _ => InvestmentStatus.PendingFounderSignatures
        };
    }

    private static bool IsStaleResponse(InvestmentStatus local, InvestmentStatus server)
    {
        if (local == InvestmentStatus.Invested && server == InvestmentStatus.FounderSignaturesReceived)
            return true;

        if (local == InvestmentStatus.Cancelled &&
            (server == InvestmentStatus.PendingFounderSignatures || server == InvestmentStatus.FounderSignaturesReceived))
            return true;

        return false;
    }

    /// <summary>
    /// Load recovery status for a specific investment from SDK.
    /// Maps all 5 boolean fields from the SDK DTO to RecoveryState.
    /// </summary>
    public async Task LoadRecoveryStatusAsync(InvestmentViewModel investment)
    {
        if (string.IsNullOrEmpty(investment.ProjectIdentifier) ||
            string.IsNullOrEmpty(investment.InvestmentWalletId))
        {
            _logger.LogWarning("LoadRecoveryStatus skipped: missing ProjectIdentifier or InvestmentWalletId");
            return;
        }

        _logger.LogInformation("Loading recovery status for project '{ProjectName}' (ID: {ProjectId}, WalletId: {WalletId})",
            investment.ProjectName, investment.ProjectIdentifier, investment.InvestmentWalletId);

        try
        {
            var request = new GetRecoveryStatus.GetRecoveryStatusRequest(
                new WalletId(investment.InvestmentWalletId),
                new ProjectId(investment.ProjectIdentifier));

            var result = await _investmentAppService.GetRecoveryStatus(request);
            if (result.IsFailure)
            {
                _logger.LogWarning("GetRecoveryStatus failed for project {ProjectId}: {Error}", investment.ProjectIdentifier, result.Error);
                return;
            }

            var recovery = result.Value.RecoveryData;

            // Update stages from recovery data
            investment.Stages.Clear();
            foreach (var item in recovery.Items)
            {
                investment.Stages.Add(new InvestmentStageViewModel
                {
                    StageNumber = item.StageIndex + 1,
                    Amount = item.Amount.ToUnitBtc().ToString("F8", CultureInfo.InvariantCulture),
                    Status = item.IsSpent ? "Released" : (recovery.HasSpendableItemsInPenalty ? "Pending" : "Not Spent")
                });
            }

            // Populate recovery details from SDK data
            investment.PenaltyDuration = recovery.PenaltyDays > 0 ? $"{recovery.PenaltyDays} days" : "";
            var daysLeft = (recovery.ExpiryDate - DateTime.UtcNow).Days;
            investment.PenaltyDaysRemaining = Math.Max(0, daysLeft);

            // Map all 5 SDK fields to RecoveryState (replaces simplified 3-state string)
            investment.RecoveryState = new RecoveryState(
                recovery.HasUnspentItems,
                recovery.HasSpendableItemsInPenalty,
                recovery.HasReleaseSignatures,
                recovery.EndOfProject,
                recovery.IsAboveThreshold);

            _logger.LogInformation("Recovery status loaded for project {ProjectId}: HasUnspent={HasUnspent}, InPenalty={InPenalty}, HasReleaseSig={HasReleaseSig}, EndOfProject={EndOfProject}, AboveThreshold={AboveThreshold}, ActionKey={ActionKey}",
                investment.ProjectIdentifier, recovery.HasUnspentItems, recovery.HasSpendableItemsInPenalty,
                recovery.HasReleaseSignatures, recovery.EndOfProject, recovery.IsAboveThreshold,
                investment.RecoveryState.ActionKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading recovery status for project {ProjectId}", investment.ProjectIdentifier);
        }
    }

    /// <summary>
    /// Build and submit a recovery transaction for an investment.
    /// </summary>
    public async Task<bool> RecoverFundsAsync(InvestmentViewModel investment, long feeRateSatsPerVByte = 20)
    {
        if (string.IsNullOrEmpty(investment.ProjectIdentifier) ||
            string.IsNullOrEmpty(investment.InvestmentWalletId)) return false;

        _logger.LogInformation("RecoverFundsAsync starting: project={ProjectId}, wallet={WalletId}, feeRate={FeeRate}",
            investment.ProjectIdentifier, investment.InvestmentWalletId, feeRateSatsPerVByte);

        try
        {
            var walletId = new WalletId(investment.InvestmentWalletId);
            var projectId = new ProjectId(investment.ProjectIdentifier);

            var buildResult = await _investmentAppService.BuildRecoveryTransaction(
                new BuildRecoveryTransaction.BuildRecoveryTransactionRequest(
                    walletId, projectId, new DomainFeerate(feeRateSatsPerVByte)));

            if (buildResult.IsFailure)
            {
                _logger.LogError("BuildRecoveryTransaction failed: {Error}", buildResult.Error);
                return false;
            }

            _logger.LogInformation("Recovery transaction built — publishing...");
            var publishResult = await _investmentAppService.SubmitTransactionFromDraft(
                new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
                    walletId.Value, projectId, buildResult.Value.TransactionDraft));

            if (publishResult.IsSuccess)
            {
                _logger.LogInformation("Recovery transaction published successfully for project {ProjectId}", investment.ProjectIdentifier);
                // Refresh recovery state from SDK after successful transaction
                await LoadRecoveryStatusAsync(investment);
                return true;
            }

            _logger.LogError("Recovery transaction publish failed: {Error}", publishResult.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RecoverFundsAsync threw exception for project {ProjectId}", investment.ProjectIdentifier);
        }

        return false;
    }

    /// <summary>
    /// Build and submit a release transaction (unfunded release / recover without penalty).
    /// </summary>
    public async Task<bool> ReleaseFundsAsync(InvestmentViewModel investment, long feeRateSatsPerVByte = 20)
    {
        if (string.IsNullOrEmpty(investment.ProjectIdentifier) ||
            string.IsNullOrEmpty(investment.InvestmentWalletId)) return false;

        _logger.LogInformation("ReleaseFundsAsync starting: project={ProjectId}, wallet={WalletId}, feeRate={FeeRate}",
            investment.ProjectIdentifier, investment.InvestmentWalletId, feeRateSatsPerVByte);

        try
        {
            var walletId = new WalletId(investment.InvestmentWalletId);
            var projectId = new ProjectId(investment.ProjectIdentifier);

            var buildResult = await _investmentAppService.BuildUnfundedReleaseTransaction(
                new BuildUnfundedReleaseTransaction.BuildUnfundedReleaseTransactionRequest(
                    walletId, projectId, new DomainFeerate(feeRateSatsPerVByte)));

            if (buildResult.IsFailure)
            {
                _logger.LogError("BuildUnfundedReleaseTransaction failed: {Error}", buildResult.Error);
                return false;
            }

            _logger.LogInformation("Release transaction built — publishing...");
            var publishResult = await _investmentAppService.SubmitTransactionFromDraft(
                new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
                    walletId.Value, projectId, buildResult.Value.TransactionDraft));

            if (publishResult.IsSuccess)
            {
                _logger.LogInformation("Release transaction published successfully for project {ProjectId}", investment.ProjectIdentifier);
                // Refresh recovery state from SDK after successful transaction
                await LoadRecoveryStatusAsync(investment);
                return true;
            }

            _logger.LogError("Release transaction publish failed: {Error}", publishResult.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReleaseFundsAsync threw exception for project {ProjectId}", investment.ProjectIdentifier);
        }

        return false;
    }

    /// <summary>
    /// Build and submit an end-of-project claim transaction.
    /// </summary>
    public async Task<bool> ClaimEndOfProjectAsync(InvestmentViewModel investment, long feeRateSatsPerVByte = 20)
    {
        if (string.IsNullOrEmpty(investment.ProjectIdentifier) ||
            string.IsNullOrEmpty(investment.InvestmentWalletId)) return false;

        _logger.LogInformation("ClaimEndOfProjectAsync starting: project={ProjectId}, wallet={WalletId}, feeRate={FeeRate}",
            investment.ProjectIdentifier, investment.InvestmentWalletId, feeRateSatsPerVByte);

        try
        {
            var walletId = new WalletId(investment.InvestmentWalletId);
            var projectId = new ProjectId(investment.ProjectIdentifier);

            var buildResult = await _investmentAppService.BuildEndOfProjectClaim(
                new BuildEndOfProjectClaim.BuildEndOfProjectClaimRequest(
                    walletId, projectId, new DomainFeerate(feeRateSatsPerVByte)));

            if (buildResult.IsFailure)
            {
                _logger.LogError("BuildEndOfProjectClaim failed: {Error}", buildResult.Error);
                return false;
            }

            _logger.LogInformation("End-of-project claim transaction built — publishing...");
            var publishResult = await _investmentAppService.SubmitTransactionFromDraft(
                new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
                    walletId.Value, projectId, buildResult.Value.TransactionDraft));

            if (publishResult.IsSuccess)
            {
                _logger.LogInformation("End-of-project claim published successfully for project {ProjectId}", investment.ProjectIdentifier);
                // Refresh recovery state from SDK after successful transaction
                await LoadRecoveryStatusAsync(investment);
                return true;
            }

            _logger.LogError("End-of-project claim publish failed: {Error}", publishResult.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClaimEndOfProjectAsync threw exception for project {ProjectId}", investment.ProjectIdentifier);
        }

        return false;
    }

    /// <summary>
    /// Build and submit a penalty release transaction (recover from penalty period).
    /// </summary>
    public async Task<bool> PenaltyReleaseFundsAsync(InvestmentViewModel investment, long feeRateSatsPerVByte = 20)
    {
        if (string.IsNullOrEmpty(investment.ProjectIdentifier) ||
            string.IsNullOrEmpty(investment.InvestmentWalletId)) return false;

        _logger.LogInformation("PenaltyReleaseFundsAsync starting: project={ProjectId}, wallet={WalletId}, feeRate={FeeRate}",
            investment.ProjectIdentifier, investment.InvestmentWalletId, feeRateSatsPerVByte);

        try
        {
            var walletId = new WalletId(investment.InvestmentWalletId);
            var projectId = new ProjectId(investment.ProjectIdentifier);

            var buildResult = await _investmentAppService.BuildPenaltyReleaseTransaction(
                new BuildPenaltyReleaseTransaction.BuildPenaltyReleaseTransactionRequest(
                    walletId, projectId, new DomainFeerate(feeRateSatsPerVByte)));

            if (buildResult.IsFailure)
            {
                _logger.LogError("BuildPenaltyReleaseTransaction failed: {Error}", buildResult.Error);
                return false;
            }

            _logger.LogInformation("Penalty release transaction built — publishing...");
            var publishResult = await _investmentAppService.SubmitTransactionFromDraft(
                new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
                    walletId.Value, projectId, buildResult.Value.TransactionDraft));

            if (publishResult.IsSuccess)
            {
                _logger.LogInformation("Penalty release published successfully for project {ProjectId}", investment.ProjectIdentifier);
                // Refresh recovery state from SDK after successful transaction
                await LoadRecoveryStatusAsync(investment);
                return true;
            }

            _logger.LogError("Penalty release publish failed: {Error}", publishResult.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PenaltyReleaseFundsAsync threw exception for project {ProjectId}", investment.ProjectIdentifier);
        }

        return false;
    }

    /// <summary>
    /// Publish an investment after founder has signed (Gap 1: ConfirmInvestment).
    /// Only valid when status is FounderSignaturesReceived (Step 2).
    /// </summary>
    public async Task<bool> ConfirmInvestmentAsync(InvestmentViewModel investment)
    {
        if (string.IsNullOrEmpty(investment.ProjectIdentifier) ||
            string.IsNullOrEmpty(investment.InvestmentWalletId) ||
            string.IsNullOrEmpty(investment.InvestmentTransactionId)) return false;

        try
        {
            // Refresh wallet UTXOs before publishing — the investment transaction was built
            // earlier (during PayWithWallet) and the UTXO state may have changed since then.
            _logger.LogInformation("Refreshing wallet {WalletId} before confirming investment {InvestmentId}...",
                investment.InvestmentWalletId, investment.InvestmentTransactionId);
            await _walletContext.RefreshAllBalancesAsync();

            var request = new PublishInvestment.PublishInvestmentRequest(
                investment.InvestmentTransactionId,
                new WalletId(investment.InvestmentWalletId),
                new ProjectId(investment.ProjectIdentifier));

            var result = await _investmentAppService.ConfirmInvestment(request);

            if (result.IsSuccess)
            {
                // Advance to Step 3 (Investment Active)
                investment.Step = 3;
                investment.StatusText = "Investment Active";
                investment.StatusClass = "active";
                investment.StatusPill2 = "Investment Active";
                investment.Status = "Active";
                return true;
            }

            _logger.LogError("ConfirmInvestment failed for project {ProjectId} and investment {InvestmentId}: {Error}",
                investment.ProjectIdentifier,
                investment.InvestmentTransactionId,
                result.Error);
            ToastRequested?.Invoke("Failed to confirm investment. Please try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ConfirmInvestmentAsync threw exception for project {ProjectId}",
                investment.ProjectIdentifier);
            ToastRequested?.Invoke("Failed to confirm investment. Please try again.");
        }

        return false;
    }

    /// <summary>
    /// Cancel a pending investment request (Gap 2: CancelInvestmentRequest).
    /// Available in PendingFounderSignatures (Step 1) and FounderSignaturesReceived (Step 2).
    /// </summary>
    public async Task<bool> CancelInvestmentAsync(InvestmentViewModel investment)
    {
        if (string.IsNullOrEmpty(investment.ProjectIdentifier) ||
            string.IsNullOrEmpty(investment.InvestmentWalletId) ||
            string.IsNullOrEmpty(investment.InvestmentTransactionId)) return false;

        try
        {
            var request = new CancelInvestmentRequest.CancelInvestmentRequestRequest(
                new WalletId(investment.InvestmentWalletId),
                new ProjectId(investment.ProjectIdentifier),
                investment.InvestmentTransactionId);

            var result = await _investmentAppService.CancelInvestmentRequest(request);

            if (result.IsSuccess)
            {
                investment.StatusText = "Cancelled";
                investment.StatusClass = "recovered";
                investment.StatusPill2 = "Cancelled";
                investment.Status = "Cancelled";
                return true;
            }

            _logger.LogError("CancelInvestmentRequest failed for project {ProjectId} and investment {InvestmentId}: {Error}",
                investment.ProjectIdentifier,
                investment.InvestmentTransactionId,
                result.Error);
            ToastRequested?.Invoke("Failed to cancel investment. Please try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CancelInvestmentAsync threw exception for project {ProjectId}",
                investment.ProjectIdentifier);
            ToastRequested?.Invoke("Failed to cancel investment. Please try again.");
        }

        return false;
    }

    /// <summary>
    /// When a signature is approved, advance the matching investment from Step 1 → Step 2.
    /// </summary>
    private void OnSignatureStatusChanged(SharedSignature sig)
    {
        var investment = Investments.FirstOrDefault(i => i.SignatureId == sig.Id);
        if (investment == null) return;

        if (sig.IsApproved && investment.Step == 1)
        {
            investment.Step = 2;
            investment.StatusText = "Transaction signed";
            investment.StatusClass = "signed";
            investment.StatusPill2 = "Transaction signed";
            investment.ApprovalStatus = "Approved";
        }
        else if (sig.IsRejected && investment.Step == 1)
        {
            investment.StatusText = "Rejected";
            investment.StatusClass = "rejected";
            investment.StatusPill2 = "Rejected";
            investment.ApprovalStatus = "Rejected";
        }
    }

    /// <summary>Clear all investments to show the empty state.</summary>
    public void ClearToEmpty()
    {
        SelectedInvestment = null;
        Investments.Clear();
        HasInvestments = false;
        FundedProjects = 0;
        TotalInvested = "0.0000";
        RecoveredToPenalty = "0.0000";
        ProjectsInRecovery = 0;
        TotalAvailable = "0.0000";
        this.RaisePropertyChanged(nameof(FundedProjects));
        this.RaisePropertyChanged(nameof(TotalInvested));
        this.RaisePropertyChanged(nameof(RecoveredToPenalty));
        this.RaisePropertyChanged(nameof(ProjectsInRecovery));
        this.RaisePropertyChanged(nameof(TotalAvailable));
    }

    public void ResetAfterDataWipe()
    {
        ClearToEmpty();
    }

    /// <summary>Navigate to investment detail view</summary>
    public void OpenInvestmentDetail(InvestmentViewModel investment)
    {
        SelectedInvestment = investment;
        // Load recovery status when detail is opened
        _ = LoadRecoveryStatusAsync(investment);
    }

    /// <summary>Navigate back to portfolio list from investment detail</summary>
    public void CloseInvestmentDetail() => SelectedInvestment = null;

    /// <summary>
    /// Create an InvestmentViewModel from a ProjectItemViewModel and an investment amount,
    /// then add it to the Investments collection. Called after a successful invest flow.
    /// </summary>
    public void AddInvestmentFromProject(ProjectItemViewModel project, string investmentAmount)
    {
        _logger.LogInformation("AddInvestmentFromProject: project='{ProjectName}' (ID: {ProjectId}), amount={Amount}",
            project.ProjectName, project.ProjectId, investmentAmount);

        var projectType = project.ProjectType.ToLowerInvariant();
        var typeEnum = ProjectTypeExtensions.FromLowerString(projectType);
        var typeLabel = ProjectTypeTerminology.AmountNoun(typeEnum);

        var stages = new ObservableCollection<InvestmentStageViewModel>();
        var stagePrefix = ProjectTypeTerminology.StageLabel(typeEnum);
        if (project.Stages.Count > 0)
        {
            foreach (var s in project.Stages)
            {
                stages.Add(new InvestmentStageViewModel
                {
                    StageNumber = s.StageNumber,
                    StagePrefix = stagePrefix,
                    Percentage = s.Percentage,
                    ReleaseDate = s.ReleaseDate,
                    Amount = s.Amount,
                    Status = "Pending"
                });
            }
        }

        var amountValue = double.TryParse(investmentAmount, System.Globalization.NumberStyles.Float,
            CultureInfo.InvariantCulture, out var parsedAmt) ? parsedAmt : 0;
        // Investment-type projects always require founder approval regardless of amount.
        // Fund-type projects compare the investment amount (in sats) against the project's
        // on-chain PenaltyThreshold. If no threshold is set (null/0), all Fund investments
        // are auto-approved.
        bool isAutoApproved;
        if (projectType == "invest")
        {
            isAutoApproved = false;
        }
        else
        {
            var amountSats = ((decimal)parsedAmt).ToUnitSatoshi();
            var thresholdSats = project.PenaltyThresholdSats ?? 0;
            isAutoApproved = thresholdSats == 0 || amountSats < thresholdSats;
        }

        var investment = new InvestmentViewModel
        {
            ProjectName = project.ProjectName,
            ShortDescription = project.ShortDescription,
            FundingAmount = $"{investmentAmount} {_currencyService.Symbol}",
            FundingDate = DateTime.Now.ToString("M/dd/yyyy"),
            TypeLabel = typeLabel,
            StatusText = isAutoApproved ? $"{typeLabel} Active" : "Awaiting Approval",
            StatusClass = isAutoApproved ? "active" : "waiting",
            StatusPill1 = typeLabel,
            StatusPill2 = isAutoApproved ? $"{typeLabel} Active" : "Awaiting Approval",
            HasPaymentPlan = true,
            PaymentSegmentsCompleted = 0,
            PaymentSegmentsTotal = stages.Count > 0 ? stages.Count : 3,
            BannerUrl = project.BannerUrl,
            AvatarUrl = project.AvatarUrl,
            TotalInvested = parsedAmt > 0 ? $"{parsedAmt:F8}" : investmentAmount,
            AvailableToClaim = "0.00000000",
            Spent = "0.00000000",
            Progress = 0,
            Status = isAutoApproved ? "Active" : "Pending",
            ProjectType = projectType,
            Step = isAutoApproved ? 3 : 1,
            TargetAmount = project.Target,
            TotalRaised = project.Raised,
            TotalInvestors = project.InvestorCount,
            StartDate = DateTime.Now.ToString("MMM dd, yyyy"),
            EndDate = project.EndDate,
            TransactionDate = DateTime.Now.ToString("MMM dd, yyyy"),
            ApprovalStatus = isAutoApproved ? "Approved" : "Pending",
            ProjectIdentifier = project.ProjectId,
            Stages = stages,
            CurrencySymbol = _currencyService.Symbol
        };

        var sig = _signatureStore.AddSignature(
            project.ProjectName,
            project.ProjectName,
            investmentAmount,
            projectType,
            project.PenaltyThresholdSats);

        investment.SignatureId = sig.Id;
        Investments.Insert(0, investment);
        HasInvestments = true;

        _logger.LogInformation("Investment added to portfolio: project='{ProjectName}', autoApproved={IsAutoApproved}, step={Step}, status='{StatusText}'",
            investment.ProjectName, investment.Step == 3, investment.Step, investment.StatusText);
    }
}

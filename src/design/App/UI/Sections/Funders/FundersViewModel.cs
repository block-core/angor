using System.Collections.ObjectModel;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Shared;
using App.UI.Shared;
using App.UI.Shared.Services;
using App.UI.Shell;
using Microsoft.Extensions.Logging;
using Nostr.Client.Utils;

namespace App.UI.Sections.Funders;

/// <summary>
/// A signature/funding request displayed in the Funders section.
/// Wraps SharedSignature data for XAML binding + UI-specific helpers.
/// Vue reference: App.vue desktop Funders page (lines 3152-3335)
/// </summary>
public partial class SignatureRequestViewModel : ReactiveObject
{
    public int Id { get; set; }
    public string ProjectTitle { get; set; } = "";
    public string Amount { get; set; } = "0.0000";
    public string Currency { get; set; } = "";
    public string Date { get; set; } = "";
    public string Time { get; set; } = "";
    /// <summary>Status: waiting, approved, rejected</summary>
    [Reactive] private string status = SignatureStatus.Waiting.ToLowerString();
    /// <summary>Whether the card detail panel is expanded (binding-driven for virtualization safety)</summary>
    [Reactive] private bool isExpanded;
    /// <summary>Investor npub key (shown in expanded details)</summary>
    public string Npub { get; set; } = "";
    /// <summary>Whether the chat has messages</summary>
    public bool HasMessages { get; set; }
    /// <summary>SDK event ID for the investment request</summary>
    public string EventId { get; set; } = "";
    /// <summary>SDK project identifier</summary>
    public string ProjectIdentifier { get; set; } = "";
    /// <summary>SDK wallet ID for the founder</summary>
    public string FounderWalletId { get; set; } = "";
    /// <summary>Investment transaction hex for approval</summary>
    public string InvestmentTransactionHex { get; set; } = "";
    /// <summary>Investor Nostr public key</summary>
    public string InvestorNostrPubKey { get; set; } = "";
    /// <summary>Investment amount in sats</summary>
    public long AmountSats { get; set; }

    /// <summary>True while the amount is still unknown (0). Happens for direct
    /// below-threshold investments when the indexer hasn't returned the funding
    /// transaction yet — the card shows a "Syncing…" affordance instead of a
    /// misleading "0.0000 BTC" until the next monitor poll fills it in.</summary>
    public bool IsAmountPending => AmountSats == 0;
    public bool IsAmountKnown => AmountSats > 0;

    /// <summary>Whether the investment transaction is confirmed on-chain (funded).</summary>
    public bool IsFunded { get; set; }

    // Status visibility helpers for XAML — reactive via [ObservableAsProperty] would be ideal
    // but since Status changes infrequently and these are re-read on each filter update,
    // we use computed getters that raise when Status changes (via [Reactive] above).
    public bool IsWaiting => Status == SignatureStatus.Waiting.ToLowerString();
    public bool IsApproved => Status == SignatureStatus.Approved.ToLowerString();
    public bool IsRejected => Status == SignatureStatus.Rejected.ToLowerString();

    /// <summary>Label for the approved state — distinguishes signed-but-unfunded from funded.</summary>
    public string ApprovedLabel => IsFunded ? "Funded" : "Awaiting funding";

}

/// <summary>
/// Funders ViewModel — renders investment requests from the shared FundersMonitor.
/// The monitor keeps a warm snapshot via background polling, so opening the tab is
/// instant; a background sync then refreshes relays and pushes updates in.
/// Approval goes through IFounderAppService.ApproveInvestment().
/// </summary>
public partial class FundersViewModel : ReactiveObject, IDisposable, INetworkSwitchAware
{
    private readonly IFounderAppService _founderAppService;
    private readonly FundersMonitor _fundersMonitor;
    private readonly ICurrencyService _currencyService;
    private readonly PrototypeSettings _settings;
    private readonly ILogger<FundersViewModel> _logger;

    [Reactive] private bool hasFunders;
    [Reactive] private string currentFilter = SignatureStatus.Waiting.ToLowerString();
    [Reactive] private bool isLoading;
    [Reactive] private bool isRefreshing;

    private List<SignatureRequestViewModel>? _cachedAllViewModels;

    public int WaitingCount => GetAllViewModels().Count(s => s.Status == SignatureStatus.Waiting.ToLowerString());
    public int ApprovedCount => GetAllViewModels().Count(s => s.Status == SignatureStatus.Approved.ToLowerString());
    public int RejectedCount => GetAllViewModels().Count(s => s.Status == SignatureStatus.Rejected.ToLowerString());
    public bool HasRejected => RejectedCount > 0;

    /// <summary>Title for the per-tab empty state. The Waiting tab distinguishes
    /// "no funders at all" from "no *new* funders" (approved ones exist).</summary>
    public string FilterEmptyTitle => CurrentFilter switch
    {
        "waiting" when ApprovedCount > 0 || RejectedCount > 0 => "No New Funders",
        "approved" when WaitingCount > 0 || RejectedCount > 0 => "No Approved Funders",
        _ => "No Funders Yet"
    };

    /// <summary>Description for the per-tab empty state.</summary>
    public string FilterEmptyDescription => CurrentFilter switch
    {
        "waiting" when ApprovedCount > 0 || RejectedCount > 0 =>
            "You're all caught up. New funding requests will appear here for your approval.",
        "approved" when WaitingCount > 0 || RejectedCount > 0 =>
            "Requests you approve will appear here.",
        _ => "When investors fund your projects, they'll appear here. You can review and approve funding requests from this page."
    };

    /// <summary>True while any load/refresh is in flight.</summary>
    public bool IsBusy => IsLoading || IsRefreshing;

    /// <summary>True once the monitor has completed at least one poll (even if empty).</summary>
    [Reactive] private bool hasEverLoaded;

    /// <summary>Full-screen loading state: only before the very first poll completes.
    /// Subsequent refreshes keep showing data/empty state with the spinning refresh icon —
    /// relay syncs can take 30s+, a full-page loader would look stuck.</summary>
    public bool ShowInitialLoading => !HasEverLoaded && IsBusy;

    /// <summary>Empty state whenever there's nothing to show and we're past the initial load.</summary>
    public bool ShowEmptyState => !HasFunders && !ShowInitialLoading;

    // SDK-loaded investment requests
    private readonly List<SignatureRequestViewModel> _sdkSignatures = new();

    /// <summary>Requests the founder rejected locally this session (UI-only state — there is
    /// no reject operation in the protocol; the request simply never gets approved).</summary>
    private readonly HashSet<string> _locallyRejected = new();

    /// <summary>Expanded card EventIds, preserved across snapshot rebuilds.</summary>
    private readonly HashSet<string> _expandedEventIds = new();

    [Reactive] private ObservableCollection<SignatureRequestViewModel> filteredSignatures = new();

    private readonly CompositeDisposable _disposables = new();

    public event Action<string>? ToastRequested;

    public FundersViewModel(
        IFounderAppService founderAppService,
        FundersMonitor fundersMonitor,
        ICurrencyService currencyService,
        PrototypeSettings settings,
        ILogger<FundersViewModel> logger)
    {
        _founderAppService = founderAppService;
        _fundersMonitor = fundersMonitor;
        _currencyService = currencyService;
        _settings = settings;
        _logger = logger;

        // Reflect settings changes (including the async load at startup) into the toggle.
        _settings.WhenAnyValue(x => x.IsAutoApproveEnabled)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(AutoApproveEnabled)))
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.CurrentFilter)
            .Subscribe(_ => UpdateFilteredSignatures())
            .DisposeWith(_disposables);

        // Keep the composed busy/empty-state properties in sync
        this.WhenAnyValue(x => x.IsLoading, x => x.IsRefreshing, x => x.HasFunders, x => x.HasEverLoaded)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsBusy));
                this.RaisePropertyChanged(nameof(ShowInitialLoading));
                this.RaisePropertyChanged(nameof(ShowEmptyState));
            })
            .DisposeWith(_disposables);

        // Live updates: whenever the monitor completes a poll, rebuild from its snapshot.
        // The monitor raises Updated on the UI thread.
        _fundersMonitor.Updated += OnMonitorUpdated;
        Disposable.Create(() => _fundersMonitor.Updated -= OnMonitorUpdated)
            .DisposeWith(_disposables);

        // Loading is started by FundersView.OnBecameActive(). Pre-warmed mobile views
        // are hidden, so eager SDK requests here compete with tab switching and can
        // make Android show an ANR while the user is on another section.
    }

    private void OnMonitorUpdated() => RebuildFromSnapshot();

    /// <summary>
    /// Founder auto-approve toggle. Persisted via <see cref="PrototypeSettings"/>;
    /// the background <see cref="FundersMonitor"/> reacts to it — switching it on
    /// triggers an immediate refresh + approval of everything pending, and every
    /// subsequent background poll approves new requests as they arrive.
    /// </summary>
    public bool AutoApproveEnabled
    {
        get => _settings.IsAutoApproveEnabled;
        set => _settings.IsAutoApproveEnabled = value;
    }

    /// <summary>
    /// Load investment requests. If the monitor already has a warm snapshot the tab
    /// renders instantly and a relay sync runs in the background; otherwise we show
    /// the loading state while the first sync completes.
    /// </summary>
    public async Task LoadInvestmentRequestsAsync()
    {
        if (_fundersMonitor.HasLoaded)
        {
            // Instant render from the warm snapshot, then await the relay sync so
            // IsRefreshing (spinner) stays active for the duration of the refresh.
            RebuildFromSnapshot();
            await _fundersMonitor.RefreshAsync();
            return;
        }

        IsLoading = true;
        try
        {
            await _fundersMonitor.RefreshAsync();
            RebuildFromSnapshot();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load funder investment requests");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Rebuild the view model list from the monitor snapshot (UI thread).</summary>
    private void RebuildFromSnapshot()
    {
        if (_fundersMonitor.HasLoaded)
            HasEverLoaded = true;

        var signatures = new List<SignatureRequestViewModel>();
        int idCounter = 10000; // high ID to avoid collision with shared store IDs

        foreach (var record in _fundersMonitor.Snapshot)
        {
            var effectiveStatus = _fundersMonitor.EffectiveStatus(record);
            var status = effectiveStatus switch
            {
                InvestmentStatus.PendingFounderSignatures => SignatureStatus.Waiting.ToLowerString(),
                InvestmentStatus.FounderSignaturesReceived or InvestmentStatus.Invested =>
                    SignatureStatus.Approved.ToLowerString(),
                InvestmentStatus.Cancelled => SignatureStatus.Rejected.ToLowerString(),
                _ => SignatureStatus.Waiting.ToLowerString()
            };

            // Session-local rejections stick until the app restarts.
            if (_locallyRejected.Contains(record.EventId))
                status = SignatureStatus.Rejected.ToLowerString();

            var amountBtc = record.AmountSats / 100_000_000.0;

            signatures.Add(new SignatureRequestViewModel
            {
                Id = idCounter++,
                ProjectTitle = record.ProjectTitle,
                Amount = amountBtc.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
                Currency = _currencyService.Symbol,
                Date = record.CreatedOn.ToString("dd MMM yyyy"),
                Time = record.CreatedOn.ToString("HH:mm"),
                Status = status,
                Npub = NostrConverter.ToNpub(record.InvestorNostrPubKey) ?? record.InvestorNostrPubKey,
                EventId = record.EventId,
                ProjectIdentifier = record.ProjectIdentifier,
                FounderWalletId = record.WalletId,
                InvestmentTransactionHex = record.InvestmentTransactionHex,
                InvestorNostrPubKey = record.InvestorNostrPubKey,
                AmountSats = record.AmountSats,
                IsFunded = record.Status == InvestmentStatus.Invested,
                IsExpanded = _expandedEventIds.Contains(record.EventId)
            });
        }

        _sdkSignatures.Clear();
        _sdkSignatures.AddRange(signatures);

        _cachedAllViewModels = null;
        UpdateFilteredSignatures();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Clears all funder state derived from the previous network. The shared
    /// <see cref="FundersMonitor"/> is reset separately by the shell; its
    /// Updated event then rebuilds this VM from the (now empty) snapshot.
    /// </remarks>
    public void ResetAfterNetworkSwitch()
    {
        _sdkSignatures.Clear();
        _locallyRejected.Clear();
        _expandedEventIds.Clear();
        _cachedAllViewModels = null;
        HasEverLoaded = false;
        IsLoading = false;
        IsRefreshing = false;
        UpdateFilteredSignatures();
    }

    /// <summary>
    /// Refresh investment requests (called by the UI refresh button).
    /// </summary>
    public async Task RefreshAsync()
    {
        if (IsRefreshing) return;
        IsRefreshing = true;
        try
        {
            await LoadInvestmentRequestsAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private List<SignatureRequestViewModel> GetAllViewModels()
    {
        if (_cachedAllViewModels != null) return _cachedAllViewModels;

        var all = new List<SignatureRequestViewModel>(_sdkSignatures);
        _cachedAllViewModels = all;
        return all;
    }

    private void UpdateFilteredSignatures()
    {
        var all = GetAllViewModels();
        var filtered = all.Where(s => s.Status == CurrentFilter).ToList();

        FilteredSignatures = new ObservableCollection<SignatureRequestViewModel>(filtered);
        HasFunders = all.Count > 0;

        this.RaisePropertyChanged(nameof(WaitingCount));
        this.RaisePropertyChanged(nameof(ApprovedCount));
        this.RaisePropertyChanged(nameof(RejectedCount));
        this.RaisePropertyChanged(nameof(HasRejected));
        this.RaisePropertyChanged(nameof(FilterEmptyTitle));
        this.RaisePropertyChanged(nameof(FilterEmptyDescription));
    }

    /// <summary>
    /// Approve a signature request. If it's an SDK investment, call IFounderAppService.ApproveInvestment().
    /// </summary>
    public void ApproveSignature(int id)
    {
        var sdkSig = _sdkSignatures.FirstOrDefault(s => s.Id == id);
        if (sdkSig != null)
        {
            _ = ApproveSignatureAsync(sdkSig);
            return;
        }
    }

    private async Task ApproveSignatureAsync(SignatureRequestViewModel sig)
    {
        if (string.IsNullOrEmpty(sig.EventId) || string.IsNullOrEmpty(sig.ProjectIdentifier) ||
            string.IsNullOrEmpty(sig.FounderWalletId)) return;

        try
        {
            var walletId = new WalletId(sig.FounderWalletId);
            var projectId = new ProjectId(sig.ProjectIdentifier);

            var investment = new Angor.Sdk.Funding.Founder.Domain.Investment(
                sig.EventId,
                DateTime.UtcNow,
                sig.InvestmentTransactionHex,
                sig.InvestorNostrPubKey,
                sig.AmountSats,
                InvestmentStatus.PendingFounderSignatures);

            var result = await _founderAppService.ApproveInvestment(
                new ApproveInvestment.ApproveInvestmentRequest(walletId, projectId, investment));

            if (result.IsSuccess)
            {
                sig.Status = SignatureStatus.Approved.ToLowerString();
                // Overlay so the next monitor poll doesn't flip the card back to
                // "waiting" before the approval DM is visible on relays.
                _fundersMonitor.MarkApprovedLocally(sig.EventId);
                _cachedAllViewModels = null;
                UpdateFilteredSignatures();
                return;
            }

            _logger.LogError(
                "ApproveInvestment failed for request {EventId} in project {ProjectId}: {Error}",
                sig.EventId,
                sig.ProjectIdentifier,
                result.Error);
            ToastRequested?.Invoke("Approval failed. Please try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to approve investment request {EventId} for project {ProjectId}",
                sig.EventId,
                sig.ProjectIdentifier);
            ToastRequested?.Invoke("Approval failed. Please try again.");
        }
    }

    public void RejectSignature(int id)
    {
        var sdkSig = _sdkSignatures.FirstOrDefault(s => s.Id == id);
        if (sdkSig != null)
        {
            sdkSig.Status = SignatureStatus.Rejected.ToLowerString();
            // UI-only: remember across monitor snapshot rebuilds for this session.
            if (sdkSig.EventId.Length > 0) _locallyRejected.Add(sdkSig.EventId);
            _cachedAllViewModels = null;
            UpdateFilteredSignatures();
            return;
        }

    }

    public void ApproveAll()
    {
        // Approve SDK signatures
        foreach (var sig in _sdkSignatures.Where(s => s.Status == SignatureStatus.Waiting.ToLowerString()).ToList())
        {
            _ = ApproveSignatureAsync(sig);
        }
        _cachedAllViewModels = null;
        UpdateFilteredSignatures();
    }

    public void ToggleExpanded(int id)
    {
        var sig = GetAllViewModels().FirstOrDefault(s => s.Id == id);
        if (sig != null)
        {
            sig.IsExpanded = !sig.IsExpanded;
            if (sig.EventId.Length > 0)
            {
                if (sig.IsExpanded) _expandedEventIds.Add(sig.EventId);
                else _expandedEventIds.Remove(sig.EventId);
            }
        }
    }

    public bool IsExpanded(int id) => GetAllViewModels().FirstOrDefault(s => s.Id == id)?.IsExpanded ?? false;

    public void SetFilter(string filter) => CurrentFilter = filter;

    public void Dispose()
    {
        _disposables.Dispose();
    }
}

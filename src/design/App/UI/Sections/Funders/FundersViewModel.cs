using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Wallet.Application;
using App.UI.Shell;
using App.UI.Shared;

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

    // Status visibility helpers for XAML — reactive via [ObservableAsProperty] would be ideal
    // but since Status changes infrequently and these are re-read on each filter update,
    // we use computed getters that raise when Status changes (via [Reactive] above).
    public bool IsWaiting => Status == SignatureStatus.Waiting.ToLowerString();
    public bool IsApproved => Status == SignatureStatus.Approved.ToLowerString();
    public bool IsRejected => Status == SignatureStatus.Rejected.ToLowerString();

    /// <summary>Create from a SharedSignature.</summary>
    public static SignatureRequestViewModel FromShared(SharedSignature sig) => new()
    {
        Id = sig.Id,
        ProjectTitle = sig.ProjectTitle,
        Amount = sig.Amount,
        Currency = sig.Currency,
        Date = sig.Date,
        Time = sig.Time,
        Status = sig.Status,
        Npub = sig.Npub,
        HasMessages = sig.HasMessages
    };
}

/// <summary>
/// Funders ViewModel — connected to SDK for loading investment requests and approval.
/// Uses IFounderAppService.GetProjectInvestments() to load pending signatures
/// and ApproveInvestment() to approve them.
/// </summary>
public partial class FundersViewModel : ReactiveObject, IDisposable
{
    private readonly IFounderAppService _founderAppService;
    private readonly IProjectAppService _projectAppService;
    private readonly IWalletAppService _walletAppService;
    private readonly SignatureStore _signatureStore;
    private readonly ICurrencyService _currencyService;

    [Reactive] private bool hasFunders;
    [Reactive] private string currentFilter = SignatureStatus.Waiting.ToLowerString();
    [Reactive] private bool isLoading;

    public ObservableCollection<int> ExpandedSignatureIds { get; } = new();

    private List<SignatureRequestViewModel>? _cachedAllViewModels;

    public int WaitingCount => GetAllViewModels().Count(s => s.Status == SignatureStatus.Waiting.ToLowerString());
    public int ApprovedCount => GetAllViewModels().Count(s => s.Status == SignatureStatus.Approved.ToLowerString());
    public int RejectedCount => GetAllViewModels().Count(s => s.Status == SignatureStatus.Rejected.ToLowerString());
    public bool HasRejected => RejectedCount > 0;

    // SDK-loaded investment requests
    private readonly List<SignatureRequestViewModel> _sdkSignatures = new();

    [Reactive] private ObservableCollection<SignatureRequestViewModel> filteredSignatures = new();

    private readonly CompositeDisposable _disposables = new();
    private readonly NotifyCollectionChangedEventHandler _collectionChangedHandler;

    public FundersViewModel(
        IFounderAppService founderAppService,
        IProjectAppService projectAppService,
        IWalletAppService walletAppService,
        SignatureStore signatureStore,
        ICurrencyService currencyService)
    {
        _founderAppService = founderAppService;
        _projectAppService = projectAppService;
        _walletAppService = walletAppService;
        _signatureStore = signatureStore;
        _currencyService = currencyService;

        this.WhenAnyValue(x => x.CurrentFilter)
            .Subscribe(_ => UpdateFilteredSignatures())
            .DisposeWith(_disposables);

        // Re-filter when the shared store changes (new investments from UI flow)
        _collectionChangedHandler = (_, _) =>
        {
            _cachedAllViewModels = null;
            UpdateFilteredSignatures();
        };
        _signatureStore.AllSignatures.CollectionChanged += _collectionChangedHandler;

        // Load investment requests from SDK
        _ = LoadInvestmentRequestsAsync();
    }

    /// <summary>
    /// Load investment requests from SDK for all founder projects.
    /// </summary>
    public async Task LoadInvestmentRequestsAsync()
    {
        IsLoading = true;

        try
        {
            var metadatasResult = await _walletAppService.GetMetadatas();
            if (metadatasResult.IsFailure) return;

            _sdkSignatures.Clear();
            int idCounter = 10000; // high ID to avoid collision with shared store IDs

            foreach (var meta in metadatasResult.Value)
            {
                // Get founder's projects
                var projectsResult = await _projectAppService.GetFounderProjects(meta.Id);
                if (projectsResult.IsFailure) continue;

                foreach (var project in projectsResult.Value.Projects)
                {
                    if (project.Id == null) continue;

                    // Get investment requests for this project
                    var investmentsResult = await _founderAppService.GetProjectInvestments(
                        new GetProjectInvestments.GetProjectInvestmentsRequest(meta.Id, project.Id));

                    if (investmentsResult.IsFailure) continue;

                    foreach (var investment in investmentsResult.Value.Investments)
                    {
                        var status = investment.Status switch
                        {
                            InvestmentStatus.PendingFounderSignatures => SignatureStatus.Waiting.ToLowerString(),
                            InvestmentStatus.FounderSignaturesReceived or InvestmentStatus.Invested =>
                                SignatureStatus.Approved.ToLowerString(),
                            InvestmentStatus.Cancelled => SignatureStatus.Rejected.ToLowerString(),
                            _ => SignatureStatus.Waiting.ToLowerString()
                        };

                        var amountBtc = (double)investment.Amount.ToUnitBtc();

                        _sdkSignatures.Add(new SignatureRequestViewModel
                        {
                            Id = idCounter++,
                            ProjectTitle = project.Name ?? "Unknown Project",
                            Amount = amountBtc.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
                            Currency = _currencyService.Symbol,
                            Date = investment.CreatedOn.ToString("MMM dd, yyyy"),
                            Time = investment.CreatedOn.ToString("HH:mm"),
                            Status = status,
                            Npub = investment.InvestorNostrPubKey ?? "",
                            EventId = investment.EventId ?? "",
                            ProjectIdentifier = project.Id.Value,
                            FounderWalletId = meta.Id.Value,
                            InvestmentTransactionHex = investment.InvestmentTransactionHex ?? "",
                            InvestorNostrPubKey = investment.InvestorNostrPubKey ?? "",
                            AmountSats = investment.Amount
                        });
                    }
                }
            }

            _cachedAllViewModels = null;
            UpdateFilteredSignatures();
        }
        catch
        {
            // SDK call failed
        }
        finally
        {
            IsLoading = false;
        }
    }

    private List<SignatureRequestViewModel> GetAllViewModels()
    {
        if (_cachedAllViewModels != null) return _cachedAllViewModels;

        var all = new List<SignatureRequestViewModel>();
        all.AddRange(_sdkSignatures);
        // Include shared store entries (from UI-only invest flow)
        foreach (var shared in _signatureStore.AllSignatures)
        {
            all.Add(SignatureRequestViewModel.FromShared(shared));
        }
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

        // Fallback to shared store
        _signatureStore.Approve(id);
        _cachedAllViewModels = null;
        UpdateFilteredSignatures();
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
                _cachedAllViewModels = null;
                UpdateFilteredSignatures();
            }
        }
        catch
        {
            // Approval failed
        }
    }

    public void RejectSignature(int id)
    {
        var sdkSig = _sdkSignatures.FirstOrDefault(s => s.Id == id);
        if (sdkSig != null)
        {
            sdkSig.Status = SignatureStatus.Rejected.ToLowerString();
            _cachedAllViewModels = null;
            UpdateFilteredSignatures();
            return;
        }

        _signatureStore.Reject(id);
        _cachedAllViewModels = null;
        UpdateFilteredSignatures();
    }

    public void ApproveAll()
    {
        // Approve SDK signatures
        foreach (var sig in _sdkSignatures.Where(s => s.Status == SignatureStatus.Waiting.ToLowerString()).ToList())
        {
            _ = ApproveSignatureAsync(sig);
        }
        // Approve shared store signatures
        _signatureStore.ApproveAll();
        _cachedAllViewModels = null;
        UpdateFilteredSignatures();
    }

    public void ToggleExpanded(int id)
    {
        if (ExpandedSignatureIds.Contains(id))
            ExpandedSignatureIds.Remove(id);
        else
            ExpandedSignatureIds.Add(id);
    }

    public bool IsExpanded(int id) => ExpandedSignatureIds.Contains(id);

    public void SetFilter(string filter) => CurrentFilter = filter;

    public void Dispose()
    {
        _disposables.Dispose();
        _signatureStore.AllSignatures.CollectionChanged -= _collectionChangedHandler;
    }
}

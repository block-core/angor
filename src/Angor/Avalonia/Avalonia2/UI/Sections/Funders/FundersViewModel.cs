using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia2.UI.Shell;
using Avalonia2.UI.Shared;

namespace Avalonia2.UI.Sections.Funders;

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
    public string Currency { get; set; } = "BTC";
    public string Date { get; set; } = "";
    public string Time { get; set; } = "";
    /// <summary>Status: waiting, approved, rejected</summary>
    [Reactive] private string status = SignatureStatus.Waiting.ToLowerString();
    /// <summary>Investor npub key (shown in expanded details)</summary>
    public string Npub { get; set; } = "npub1aunjpz36t2vwtqxyph2jc30c4feng4gv5yhhw6yckgzxa0rn52tq7tsnm7";
    /// <summary>Whether the chat has messages</summary>
    public bool HasMessages { get; set; }

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
/// Funders ViewModel — founder's view of incoming signature/funding requests.
/// Reads from SharedViewModels.Signatures (shared store) so signatures created
/// during the invest flow appear here. Also includes hardcoded sample data.
/// Vue reference: App.vue desktop Funders page (lines 3152-3335).
/// </summary>
public partial class FundersViewModel : ReactiveObject, IDisposable
{
    [Reactive] private bool hasFunders = true;

    /// <summary>
    /// Current filter tab: waiting, approved, rejected.
    /// Vue: funderFilter reactive variable.
    /// </summary>
    [Reactive] private string currentFilter = SignatureStatus.Waiting.ToLowerString();

    /// <summary>
    /// Tracks which signature cards are expanded (showing npub).
    /// </summary>
    public ObservableCollection<int> ExpandedSignatureIds { get; } = new();

    // Cached list of all VMs — invalidated on collection/toggle changes, avoids repeated allocation.
    private List<SignatureRequestViewModel>? _cachedAllViewModels;

    // ── Signature counts (Vue: signatureCounts) ──
    public int WaitingCount => GetAllViewModels().Count(s => s.Status == SignatureStatus.Waiting.ToLowerString());
    public int ApprovedCount => GetAllViewModels().Count(s => s.Status == SignatureStatus.Approved.ToLowerString());
    public int RejectedCount => GetAllViewModels().Count(s => s.Status == SignatureStatus.Rejected.ToLowerString());
    public bool HasRejected => RejectedCount > 0;

    // ── Sample signatures (always present as demo data) ──
    private readonly List<SignatureRequestViewModel> _sampleSignatures = new()
    {
        new SignatureRequestViewModel
        {
            Id = 1,
            ProjectTitle = "Hope With \u20bfitcoin",
            Amount = "0.5000",
            Currency = "BTC",
            Date = "Feb 25, 2026",
            Time = "14:30",
            Status = SignatureStatus.Waiting.ToLowerString(),
            Npub = "npub1q8s7k4x9z2m3n4p5r6t7u8v9w0x1y2z3a4b5c6d7e8f",
            HasMessages = true
        },
        new SignatureRequestViewModel
        {
            Id = 2,
            ProjectTitle = "Hope With \u20bfitcoin",
            Amount = "0.7500",
            Currency = "BTC",
            Date = "Feb 20, 2026",
            Time = "09:15",
            Status = SignatureStatus.Waiting.ToLowerString(),
            Npub = "npub1m2d9f3a5b6c7d8e9f0g1h2i3j4k5l6m7n8o9p0q1r",
            HasMessages = false
        },
        new SignatureRequestViewModel
        {
            Id = 3,
            ProjectTitle = "Bitcoin Education Hub",
            Amount = "1.2500",
            Currency = "BTC",
            Date = "Feb 18, 2026",
            Time = "11:45",
            Status = SignatureStatus.Approved.ToLowerString(),
            Npub = "npub1x7r2c5b4d6e8f0a1b3c5d7e9f1g3h5i7j9k1l3m5",
            HasMessages = true
        },
        new SignatureRequestViewModel
        {
            Id = 4,
            ProjectTitle = "Hope With \u20bfitcoin",
            Amount = "0.2500",
            Currency = "BTC",
            Date = "Feb 15, 2026",
            Time = "16:20",
            Status = SignatureStatus.Rejected.ToLowerString(),
            Npub = "npub1a2b3c4d5e6f7g8h9i0j1k2l3m4n5o6p7q8r9s0t1u",
            HasMessages = false
        }
    };

    /// <summary>
    /// Filtered signatures based on current filter tab.
    /// </summary>
    [Reactive] private ObservableCollection<SignatureRequestViewModel> filteredSignatures = new();

    private readonly CompositeDisposable _disposables = new();

    // Track the CollectionChanged handler so we can unsubscribe
    private readonly NotifyCollectionChangedEventHandler _collectionChangedHandler;

    public FundersViewModel()
    {
        // React to filter changes — WhenAnyValue emits the initial value immediately,
        // so this also handles the first call to UpdateFilteredSignatures().
        this.WhenAnyValue(x => x.CurrentFilter)
            .Subscribe(_ => UpdateFilteredSignatures())
            .DisposeWith(_disposables);

        // Re-filter when the shared store changes (new investments)
        _collectionChangedHandler = (_, _) =>
        {
            _cachedAllViewModels = null; // invalidate cache
            UpdateFilteredSignatures();
        };
        SharedViewModels.Signatures.AllSignatures.CollectionChanged += _collectionChangedHandler;

        // React to prototype toggle (show populated vs empty).
        // Skip(1) to avoid double-processing — the initial filter subscription above
        // already called UpdateFilteredSignatures with the current toggle state.
        SharedViewModels.Prototype.WhenAnyValue(x => x.ShowPopulatedApp)
            .Skip(1)
            .Subscribe(_ =>
            {
                _cachedAllViewModels = null; // invalidate cache
                UpdateFilteredSignatures();
            })
            .DisposeWith(_disposables);
    }

    /// <summary>
    /// Get all signature view models: sample data + shared store entries.
    /// When ShowPopulatedApp is false, only include shared store entries (user-created).
    /// Results are cached and invalidated when the collection or toggle changes.
    /// </summary>
    private List<SignatureRequestViewModel> GetAllViewModels()
    {
        if (_cachedAllViewModels != null) return _cachedAllViewModels;

        var all = new List<SignatureRequestViewModel>();
        if (SharedViewModels.Prototype.ShowPopulatedApp)
        {
            all.AddRange(_sampleSignatures);
        }
        foreach (var shared in SharedViewModels.Signatures.AllSignatures)
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

        // Replace entire collection — fires single CollectionChanged(Reset) instead of N+1 events
        FilteredSignatures = new ObservableCollection<SignatureRequestViewModel>(filtered);

        HasFunders = all.Count > 0;

        this.RaisePropertyChanged(nameof(WaitingCount));
        this.RaisePropertyChanged(nameof(ApprovedCount));
        this.RaisePropertyChanged(nameof(RejectedCount));
        this.RaisePropertyChanged(nameof(HasRejected));
    }

    public void ApproveSignature(int id)
    {
        // Check sample signatures first
        var sampleSig = _sampleSignatures.FirstOrDefault(s => s.Id == id);
        if (sampleSig != null)
        {
            sampleSig.Status = SignatureStatus.Approved.ToLowerString();
            _cachedAllViewModels = null;
            UpdateFilteredSignatures();
            return;
        }

        // Otherwise delegate to shared store (which fires SignatureStatusChanged event)
        SharedViewModels.Signatures.Approve(id);
        _cachedAllViewModels = null;
        UpdateFilteredSignatures();
    }

    public void RejectSignature(int id)
    {
        var sampleSig = _sampleSignatures.FirstOrDefault(s => s.Id == id);
        if (sampleSig != null)
        {
            sampleSig.Status = SignatureStatus.Rejected.ToLowerString();
            _cachedAllViewModels = null;
            UpdateFilteredSignatures();
            return;
        }

        SharedViewModels.Signatures.Reject(id);
        _cachedAllViewModels = null;
        UpdateFilteredSignatures();
    }

    public void ApproveAll()
    {
        // Approve sample signatures
        foreach (var sig in _sampleSignatures.Where(s => s.Status == SignatureStatus.Waiting.ToLowerString()).ToList())
        {
            sig.Status = SignatureStatus.Approved.ToLowerString();
        }
        // Approve shared store signatures
        SharedViewModels.Signatures.ApproveAll();
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

    public void SetFilter(string filter)
    {
        CurrentFilter = filter;
    }

    public void Dispose()
    {
        _disposables.Dispose();
        SharedViewModels.Signatures.AllSignatures.CollectionChanged -= _collectionChangedHandler;
    }
}

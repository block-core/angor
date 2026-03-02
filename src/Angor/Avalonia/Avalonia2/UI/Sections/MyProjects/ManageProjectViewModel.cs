using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Avalonia2.UI.Sections.MyProjects;

/// <summary>
/// Represents a single UTXO transaction in the claim/release/spent modals.
/// Vue ref: ManageFunds.vue lines ~300-400 (available-transaction-item, spent-transaction-item).
/// </summary>
public class UtxoTransactionViewModel : INotifyPropertyChanged
{
    public string TxId { get; set; } = "";
    public string Amount { get; set; } = "0";
    public bool IsSpent { get; set; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// A single stage in the ManageProject view.
/// Vue ref: ManageFunds.vue lines 1149-1229 (default stages).
/// </summary>
public class ManageStageViewModel
{
    public int Number { get; set; }
    public string AmountLeft { get; set; } = "0";
    public int UtxoCount { get; set; }
    public string CompletionDate { get; set; } = "";
    public bool Available { get; set; }
    public bool CanClaim { get; set; }
    public int? DaysUntilAvailable { get; set; }

    /// <summary>Number of spent transactions (for "Spent by founder" badge)</summary>
    public int SpentTransactionCount { get; set; }
    /// <summary>Number of unspent transactions (for "Funds Available" badge)</summary>
    public int UnspentTransactionCount { get; set; }

    /// <summary>Available (unspent) UTXO transactions for this stage.</summary>
    public ObservableCollection<UtxoTransactionViewModel> AvailableTransactions { get; set; } = new();

    /// <summary>Spent UTXO transactions for this stage.</summary>
    public ObservableCollection<UtxoTransactionViewModel> SpentTransactions { get; set; } = new();

    /// <summary>True when amount is 0 and all transactions are spent.</summary>
    public bool IsFullySpent => AmountLeft == "0" && SpentTransactionCount > 0 && UnspentTransactionCount == 0;

    /// <summary>True when there are unspent transactions available.</summary>
    public bool HasUnspentTransactions => UnspentTransactionCount > 0;

    /// <summary>
    /// Button display mode:
    /// - "Claim" when available and canClaim
    /// - "Spent" when fully spent (all transactions spent)
    /// - "AvailableInDays" when available but !canClaim (has daysUntilAvailable)
    /// - "None" otherwise
    /// </summary>
    public string ButtonMode
    {
        get
        {
            if (Available && CanClaim) return "Claim";
            if (IsFullySpent) return "Spent";
            if (Available && !CanClaim) return "AvailableInDays";
            return "None";
        }
    }

    /// <summary>Text for the "Available in X Days" button</summary>
    public string AvailableInDaysText => DaysUntilAvailable.HasValue
        ? $"Available in {DaysUntilAvailable.Value} Days"
        : "Available";

    /// <summary>True when the "Available in X Days" disabled button should show.
    /// Vue logic: stage.available && !stage.canClaim && !hasAllTransactionsSpent</summary>
    public bool ShowAvailableInDays => Available && !CanClaim && !IsFullySpent;
}

/// <summary>
/// ViewModel for the Manage Project detail screen (founder's view).
/// Vue ref: ManageFunds.vue — visual-only, all data hardcoded.
/// </summary>
public partial class ManageProjectViewModel : ReactiveObject
{
    /// <summary>The project being managed (from MyProjectsView).</summary>
    public MyProjectItemViewModel Project { get; }

    // ── Project Statistics (Vue lines 1072-1075) ──
    public string TotalInvestment { get; } = "0.0217701";
    public string AvailableBalance { get; } = "0.00544254";
    public string Withdrawable { get; } = "0";
    public int TotalStages { get; } = 4;

    // ── Project ID (Vue line 1069) ──
    public string ProjectId { get; } = "angor1qc8jlugwgp90vzkhf336d8exldhwd8z5u4ssaen";

    // ── Private Keys Data (Vue ManageFunds.vue lines 634-818, stubbed) ──
    public string FounderKey { get; } = "02a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2";
    public string RecoveryKey { get; } = "03f1e2d3c4b5a6f7e8d9c0b1a2f3e4d5c6b7a8f9e0d1c2b3a4f5e6d7c8b9a0f1e2";
    public string NostrNpub { get; } = "npub1qkx8rft57j4wz2k3l8m9n0p5q6r7s8t9u0v1w2x3y4z5a6b7c8d9e0f1g2h3";
    public string Nip05 { get; } = "angor_project@nostr.angor.io";
    public string NostrNsec { get; } = "nsec1a2b3c4d5e6f7g8h9i0j1k2l3m4n5o6p7q8r9s0t1u2v3w4x5y6z7a8b9c0d1";
    public string NostrHex { get; } = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    // ── Next Stage Countdown (Vue lines 1103-1107) ──
    public int CountdownDays { get; } = 9;
    public int CountdownHours { get; } = 13;
    public int CountdownMins { get; } = 38;

    // ── Transaction Statistics (computed from stages: total=5, spent=3, available=2) ──
    public int TransactionTotal { get; } = 5;
    public int TransactionSpent { get; } = 3;
    public int TransactionAvailable { get; } = 2;

    // ── Stages (Vue lines 1149-1229, default project) ──
    public ObservableCollection<ManageStageViewModel> Stages { get; }

    // ── Modal State ──

    /// <summary>True when the project didn't meet its target — shows "Release Funds" flow instead of "Claim".</summary>
    [Reactive] public partial bool ProjectDidntMeetTarget { get; set; }

    /// <summary>True when funds have been released back to investors.</summary>
    [Reactive] public partial bool FundsReleasedToInvestors { get; set; }

    /// <summary>Index of the stage currently selected for claim/spent/release modals.</summary>
    [Reactive] public partial int SelectedStageIndex { get; set; }

    // ── Claim Flow Modals ──
    [Reactive] public partial bool ShowClaimModal { get; set; }
    [Reactive] public partial bool ShowPasswordModal { get; set; }
    [Reactive] public partial bool ShowSuccessModal { get; set; }

    // ── Release Funds Flow Modals ──
    [Reactive] public partial bool ShowReleaseFundsModal { get; set; }
    [Reactive] public partial bool ShowReleaseFundsPasswordModal { get; set; }
    [Reactive] public partial bool ShowReleaseFundsSuccessModal { get; set; }

    // ── Spent/Returned Modals ──
    [Reactive] public partial bool ShowSpentModal { get; set; }

    // ── Form State ──
    [Reactive] public partial string WalletPassword { get; set; }
    [Reactive] public partial bool IsClaiming { get; set; }
    [Reactive] public partial bool IsReleasingFunds { get; set; }
    [Reactive] public partial string ClaimedAmount { get; set; }
    [Reactive] public partial string ReleasedAmount { get; set; }

    /// <summary>Returns the currently selected stage, or null.</summary>
    public ManageStageViewModel? SelectedStage =>
        SelectedStageIndex >= 0 && SelectedStageIndex < Stages.Count
            ? Stages[SelectedStageIndex]
            : null;

    public ManageProjectViewModel(MyProjectItemViewModel project)
    {
        Project = project;
        WalletPassword = "";
        ClaimedAmount = "0";
        ReleasedAmount = "0";

        Stages = new ObservableCollection<ManageStageViewModel>
        {
            new()
            {
                Number = 1,
                AmountLeft = "0",
                UtxoCount = 0,
                CompletionDate = "27 October 2025",
                Available = false,
                CanClaim = false,
                SpentTransactionCount = 1,
                UnspentTransactionCount = 0,
                SpentTransactions = new ObservableCollection<UtxoTransactionViewModel>
                {
                    new() { TxId = "a1b2c3d4e5f6...7890abcd", Amount = "0.00544254", IsSpent = true }
                }
            },
            new()
            {
                Number = 2,
                AmountLeft = "0",
                UtxoCount = 0,
                CompletionDate = "10 November 2025",
                Available = false,
                CanClaim = false,
                SpentTransactionCount = 2,
                UnspentTransactionCount = 0,
                SpentTransactions = new ObservableCollection<UtxoTransactionViewModel>
                {
                    new() { TxId = "f8e7d6c5b4a3...2109fedc", Amount = "0.00326553", IsSpent = true },
                    new() { TxId = "1a2b3c4d5e6f...7089abef", Amount = "0.00217701", IsSpent = true }
                }
            },
            new()
            {
                Number = 3,
                AmountLeft = "0.00450000",
                UtxoCount = 1,
                CompletionDate = "24 November 2025",
                Available = true,
                CanClaim = true,
                SpentTransactionCount = 0,
                UnspentTransactionCount = 1,
                AvailableTransactions = new ObservableCollection<UtxoTransactionViewModel>
                {
                    new() { TxId = "9f8e7d6c5b4a...3210dcba", Amount = "0.00450000", IsSpent = false }
                }
            },
            new()
            {
                Number = 4,
                AmountLeft = "0.00544254",
                UtxoCount = 1,
                CompletionDate = "08 December 2025",
                Available = true,
                CanClaim = false,
                DaysUntilAvailable = 9,
                SpentTransactionCount = 0,
                UnspentTransactionCount = 1,
                AvailableTransactions = new ObservableCollection<UtxoTransactionViewModel>
                {
                    new() { TxId = "b3c4d5e6f7a8...9012efgh", Amount = "0.00544254", IsSpent = false }
                }
            }
        };
    }
}

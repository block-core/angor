using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Shared;
using Avalonia2.Composition;
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
/// Connected to SDK for claimable transactions and fund release operations.
/// </summary>
public partial class ManageProjectViewModel : ReactiveObject
{
    private readonly IFounderAppService _founderAppService;

    /// <summary>The project being managed (from MyProjectsView).</summary>
    public MyProjectItemViewModel Project { get; }

    // ── Project Statistics ──
    public string TotalInvestment { get; private set; } = "0.0000";
    public string AvailableBalance { get; private set; } = "0.0000";
    public string Withdrawable { get; private set; } = "0";
    public int TotalStages { get; private set; }

    // ── Project ID ──
    public string ProjectId => Project.ProjectIdentifier;

    // ── Private Keys Data (placeholder — SDK doesn't expose these directly) ──
    public string FounderKey { get; } = "";
    public string RecoveryKey { get; } = "";
    public string NostrNpub { get; } = "";
    public string Nip05 { get; } = "";
    public string NostrNsec { get; } = "";
    public string NostrHex { get; } = "";

    // ── Next Stage Countdown ──
    public int CountdownDays { get; private set; }
    public int CountdownHours { get; private set; }
    public int CountdownMins { get; private set; }

    // ── Transaction Statistics ──
    public int TransactionTotal { get; private set; }
    public int TransactionSpent { get; private set; }
    public int TransactionAvailable { get; private set; }

    // ── Stages ──
    public ObservableCollection<ManageStageViewModel> Stages { get; } = new();

    // ── Modal State ──
    [Reactive] public partial bool ProjectDidntMeetTarget { get; set; }
    [Reactive] public partial bool FundsReleasedToInvestors { get; set; }
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

    public ManageStageViewModel? SelectedStage =>
        SelectedStageIndex >= 0 && SelectedStageIndex < Stages.Count
            ? Stages[SelectedStageIndex]
            : null;

    public ManageProjectViewModel(MyProjectItemViewModel project)
    {
        _founderAppService = ServiceLocator.FounderApp;
        Project = project;
        WalletPassword = "";
        ClaimedAmount = "0";
        ReleasedAmount = "0";

        // Load claimable transactions from SDK
        _ = LoadClaimableTransactionsAsync();
    }

    /// <summary>
    /// Load claimable transactions from SDK.
    /// </summary>
    public async Task LoadClaimableTransactionsAsync()
    {
        if (string.IsNullOrEmpty(Project.ProjectIdentifier) ||
            string.IsNullOrEmpty(Project.OwnerWalletId)) return;

        try
        {
            var walletId = new WalletId(Project.OwnerWalletId);
            var projectId = new ProjectId(Project.ProjectIdentifier);

            var result = await _founderAppService.GetClaimableTransactions(
                new GetClaimableTransactions.GetClaimableTransactionsRequest(walletId, projectId));

            if (result.IsFailure) return;

            Stages.Clear();
            var transactions = result.Value.Transactions.ToList();
            double totalAmount = 0;
            double availableAmount = 0;
            int spentCount = 0;
            int availableCount = 0;

            // Group by stage
            var stageGroups = transactions.GroupBy(t => t.StageNumber).OrderBy(g => g.Key);
            foreach (var group in stageGroups)
            {
                var stageTransactions = group.ToList();
                var stageAmount = stageTransactions.Sum(t => t.Amount.Sats / 100_000_000.0);
                totalAmount += stageAmount;

                var claimable = stageTransactions.Where(t => t.ClaimStatus == Angor.Sdk.Funding.Founder.Dtos.ClaimStatus.Unspent).ToList();
                var spent = stageTransactions.Where(t => t.ClaimStatus == Angor.Sdk.Funding.Founder.Dtos.ClaimStatus.SpentByFounder).ToList();

                availableAmount += claimable.Sum(t => t.Amount.Sats / 100_000_000.0);
                availableCount += claimable.Count;
                spentCount += spent.Count;

                var stage = new ManageStageViewModel
                {
                    Number = group.Key,
                    AmountLeft = claimable.Sum(t => t.Amount.Sats / 100_000_000.0).ToString("F8", CultureInfo.InvariantCulture),
                    UtxoCount = stageTransactions.Count,
                    CompletionDate = stageTransactions.FirstOrDefault()?.DynamicReleaseDate?.ToString("dd MMMM yyyy") ?? "",
                    Available = claimable.Count > 0,
                    CanClaim = claimable.Count > 0,
                    SpentTransactionCount = spent.Count,
                    UnspentTransactionCount = claimable.Count,
                };

                foreach (var tx in claimable)
                {
                    stage.AvailableTransactions.Add(new UtxoTransactionViewModel
                    {
                        TxId = tx.InvestorAddress,
                        Amount = (tx.Amount.Sats / 100_000_000.0).ToString("F8", CultureInfo.InvariantCulture),
                        IsSpent = false
                    });
                }

                foreach (var tx in spent)
                {
                    stage.SpentTransactions.Add(new UtxoTransactionViewModel
                    {
                        TxId = tx.InvestorAddress,
                        Amount = (tx.Amount.Sats / 100_000_000.0).ToString("F8", CultureInfo.InvariantCulture),
                        IsSpent = true
                    });
                }

                Stages.Add(stage);
            }

            TotalInvestment = totalAmount.ToString("F8", CultureInfo.InvariantCulture);
            AvailableBalance = availableAmount.ToString("F8", CultureInfo.InvariantCulture);
            TotalStages = Stages.Count;
            TransactionTotal = transactions.Count;
            TransactionSpent = spentCount;
            TransactionAvailable = availableCount;

            this.RaisePropertyChanged(nameof(TotalInvestment));
            this.RaisePropertyChanged(nameof(AvailableBalance));
            this.RaisePropertyChanged(nameof(TotalStages));
            this.RaisePropertyChanged(nameof(TransactionTotal));
            this.RaisePropertyChanged(nameof(TransactionSpent));
            this.RaisePropertyChanged(nameof(TransactionAvailable));
        }
        catch
        {
            // SDK call failed
        }
    }

    /// <summary>
    /// Release funds back to investors for releasable transactions.
    /// </summary>
    public async Task<bool> ReleaseFundsToInvestorsAsync()
    {
        if (string.IsNullOrEmpty(Project.ProjectIdentifier) ||
            string.IsNullOrEmpty(Project.OwnerWalletId)) return false;

        try
        {
            var walletId = new WalletId(Project.OwnerWalletId);
            var projectId = new ProjectId(Project.ProjectIdentifier);

            // Get releasable transactions
            var releasableResult = await _founderAppService.GetReleasableTransactions(
                new GetReleasableTransactions.GetReleasableTransactionsRequest(walletId, projectId));

            if (releasableResult.IsFailure) return false;

            var eventIds = releasableResult.Value.Transactions
                .Where(t => t.Released == null)
                .Select(t => t.InvestmentEventId)
                .ToList();

            if (eventIds.Count == 0) return false;

            var releaseResult = await _founderAppService.ReleaseFunds(
                new ReleaseFunds.ReleaseFundsRequest(walletId, projectId, eventIds));

            if (releaseResult.IsSuccess)
            {
                FundsReleasedToInvestors = true;
                await LoadClaimableTransactionsAsync();
                return true;
            }
        }
        catch { }

        return false;
    }
}

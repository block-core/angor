using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Nostr.Client.Utils;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using App.UI.Shared;

namespace App.UI.Sections.MyProjects;

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
    private readonly IProjectAppService _projectAppService;
    private readonly IProjectService _projectService;
    private readonly ICurrencyService _currencyService;
    private readonly ILogger<ManageProjectViewModel> _logger;

    public event Action<string>? ToastRequested;

    /// <summary>Currency symbol from ICurrencyService (e.g. "BTC", "TBTC").</summary>
    public string CurrencySymbol => _currencyService.Symbol;

    /// <summary>The project being managed (from MyProjectsView).</summary>
    public MyProjectItemViewModel Project { get; }

    // ── Project Statistics ──
    public string TotalInvestment { get; private set; } = "0.0000";
    public string AvailableBalance { get; private set; } = "0.0000";
    public string Withdrawable { get; private set; } = "0";
    public int TotalStages { get; private set; }

    // ── Project ID ──
    public string ProjectId => Project.ProjectIdentifier;

    // ── Private Keys Data ──
    [Reactive] public partial string FounderKey { get; set; }
    [Reactive] public partial string RecoveryKey { get; set; }
    [Reactive] public partial string NostrNpub { get; set; }
    [Reactive] public partial string Nip05 { get; set; }
    [Reactive] public partial string NostrNsec { get; set; }
    [Reactive] public partial string NostrHex { get; set; }

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

    public ManageProjectViewModel(
        MyProjectItemViewModel project,
        IFounderAppService founderAppService,
        IProjectAppService projectAppService,
        IProjectService projectService,
        ICurrencyService currencyService,
        ILogger<ManageProjectViewModel> logger)
    {
        _founderAppService = founderAppService;
        _projectAppService = projectAppService;
        _projectService = projectService;
        _currencyService = currencyService;
        _logger = logger;
        Project = project;
        WalletPassword = "";
        ClaimedAmount = "0";
        ReleasedAmount = "0";
        FounderKey = "";
        RecoveryKey = "";
        NostrNpub = "";
        Nip05 = "";
        NostrNsec = "";
        NostrHex = "";

        // Load claimable transactions and project keys from SDK
        _ = LoadClaimableTransactionsAsync();
        _ = LoadProjectKeysAsync();
        _ = LoadProjectStatisticsAsync();
    }

    /// <summary>
    /// Load public project keys from SDK via IProjectService.
    /// </summary>
    private async Task LoadProjectKeysAsync()
    {
        if (string.IsNullOrEmpty(Project.ProjectIdentifier)) return;

        try
        {
            var projectResult = await _projectService.GetAsync(new ProjectId(Project.ProjectIdentifier));
            if (projectResult.IsFailure) return;

            var project = projectResult.Value;
            FounderKey = project.FounderKey ?? "";
            RecoveryKey = project.FounderRecoveryKey ?? "";

            if (!string.IsNullOrEmpty(project.NostrPubKey))
            {
                NostrNpub = NostrConverter.ToNpub(project.NostrPubKey) ?? project.NostrPubKey;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load project keys for project {ProjectId}", Project.ProjectIdentifier);
        }
    }

    /// <summary>
    /// Load project statistics from the SDK (total invested, investor count, etc.).
    /// Provides more accurate header stats than manual computation from claimable transactions.
    /// </summary>
    private async Task LoadProjectStatisticsAsync()
    {
        if (string.IsNullOrEmpty(Project.ProjectIdentifier)) return;

        try
        {
            var projectId = new ProjectId(Project.ProjectIdentifier);
            var statsResult = await _projectAppService.GetProjectStatistics(projectId);
            if (statsResult.IsFailure) return;

            var stats = statsResult.Value;

            TotalInvestment = stats.TotalInvested.ToUnitBtc().ToString("F8", CultureInfo.InvariantCulture);
            AvailableBalance = stats.AvailableBalance.ToUnitBtc().ToString("F8", CultureInfo.InvariantCulture);
            Withdrawable = stats.WithdrawableAmount.ToUnitBtc().ToString("F8", CultureInfo.InvariantCulture);
            TotalStages = stats.TotalStages;
            TransactionTotal = stats.TotalTransactions;
            TransactionSpent = stats.SpentTransactions;
            TransactionAvailable = stats.AvailableTransactions;

            this.RaisePropertyChanged(nameof(TotalInvestment));
            this.RaisePropertyChanged(nameof(AvailableBalance));
            this.RaisePropertyChanged(nameof(Withdrawable));
            this.RaisePropertyChanged(nameof(TotalStages));
            this.RaisePropertyChanged(nameof(TransactionTotal));
            this.RaisePropertyChanged(nameof(TransactionSpent));
            this.RaisePropertyChanged(nameof(TransactionAvailable));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load project statistics for project {ProjectId}", Project.ProjectIdentifier);
        }
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
                var stageAmount = (double)stageTransactions.Sum(t => t.Amount.Sats.ToUnitBtc());
                totalAmount += stageAmount;

                var claimable = stageTransactions.Where(t => t.ClaimStatus == Angor.Sdk.Funding.Founder.Dtos.ClaimStatus.Unspent).ToList();
                var spent = stageTransactions.Where(t => t.ClaimStatus == Angor.Sdk.Funding.Founder.Dtos.ClaimStatus.SpentByFounder).ToList();

                availableAmount += (double)claimable.Sum(t => t.Amount.Sats.ToUnitBtc());
                availableCount += claimable.Count;
                spentCount += spent.Count;

                var stage = new ManageStageViewModel
                {
                    Number = group.Key,
                    AmountLeft = claimable.Sum(t => t.Amount.Sats.ToUnitBtc()).ToString("F8", CultureInfo.InvariantCulture),
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
                        Amount = tx.Amount.Sats.ToUnitBtc().ToString("F8", CultureInfo.InvariantCulture),
                        IsSpent = false
                    });
                }

                foreach (var tx in spent)
                {
                    stage.SpentTransactions.Add(new UtxoTransactionViewModel
                    {
                        TxId = tx.InvestorAddress,
                        Amount = tx.Amount.Sats.ToUnitBtc().ToString("F8", CultureInfo.InvariantCulture),
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load claimable transactions for project {ProjectId}", Project.ProjectIdentifier);
        }
    }

    /// <summary>
    /// Claim (spend) stage funds for selected transactions.
    /// Builds a spending transaction and broadcasts it via the SDK.
    /// </summary>
    public async Task<bool> ClaimStageFundsAsync(int stageNumber, IEnumerable<UtxoTransactionViewModel> selectedTransactions, long feeRateSatsPerVByte = 20)
    {
        if (string.IsNullOrEmpty(Project.ProjectIdentifier) ||
            string.IsNullOrEmpty(Project.OwnerWalletId)) return false;

        if (stageNumber <= 0) return false;

        try
        {
            var walletId = new WalletId(Project.OwnerWalletId);
            var projectId = new ProjectId(Project.ProjectIdentifier);
            var stageIndex = stageNumber - 1;

            var toSpend = selectedTransactions.Select(t => new SpendTransactionDto
            {
                InvestorAddress = t.TxId,
                StageId = stageIndex
            });

            var fee = new Angor.Shared.Models.FeeEstimation { FeeRate = feeRateSatsPerVByte, Confirmations = 1 };

            var spendResult = await _founderAppService.SpendStageFunds(
                new SpendStageFunds.SpendStageFundsRequest(walletId, projectId, fee, toSpend));

            if (spendResult.IsFailure)
            {
                _logger.LogError("SpendStageFunds failed for project {ProjectId}: {Error}", Project.ProjectIdentifier,
                    spendResult.Error);
                ToastRequested?.Invoke("Failed to claim stage funds. Please try again.");
                return false;
            }

            var publishResult = await _founderAppService.SubmitTransactionFromDraft(
                new PublishFounderTransaction.PublishFounderTransactionRequest(spendResult.Value.TransactionDraft));

            if (publishResult.IsSuccess)
            {
                await LoadClaimableTransactionsAsync();
                return true;
            }

            _logger.LogError("SubmitTransactionFromDraft failed while claiming stage funds for project {ProjectId}: {Error}",
                Project.ProjectIdentifier,
                publishResult.Error);
            ToastRequested?.Invoke("Failed to claim stage funds. Please try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClaimStageFundsAsync threw exception for project {ProjectId}", Project.ProjectIdentifier);
            ToastRequested?.Invoke("Failed to claim stage funds. Please try again.");
        }

        return false;
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

            if (releasableResult.IsFailure)
            {
                _logger.LogError("GetReleasableTransactions failed for project {ProjectId}: {Error}",
                    Project.ProjectIdentifier,
                    releasableResult.Error);
                ToastRequested?.Invoke("Failed to release funds to investors. Please try again.");
                return false;
            }

            var eventIds = releasableResult.Value.Transactions
                .Where(t => t.Released == null)
                .Select(t => t.InvestmentEventId)
                .ToList();

            if (eventIds.Count == 0)
            {
                _logger.LogWarning("ReleaseFundsToInvestorsAsync found no releasable transactions for project {ProjectId}",
                    Project.ProjectIdentifier);
                ToastRequested?.Invoke("No releasable investor funds were found.");
                return false;
            }

            var releaseResult = await _founderAppService.ReleaseFunds(
                new ReleaseFunds.ReleaseFundsRequest(walletId, projectId, eventIds));

            if (releaseResult.IsSuccess)
            {
                FundsReleasedToInvestors = true;
                await LoadClaimableTransactionsAsync();
                return true;
            }

            _logger.LogError("ReleaseFunds failed for project {ProjectId}: {Error}", Project.ProjectIdentifier,
                releaseResult.Error);
            ToastRequested?.Invoke("Failed to release funds to investors. Please try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReleaseFundsToInvestorsAsync threw exception for project {ProjectId}",
                Project.ProjectIdentifier);
            ToastRequested?.Invoke("Failed to release funds to investors. Please try again.");
        }

        return false;
    }
}

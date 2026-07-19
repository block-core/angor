using System.Threading;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Shared;
using App.UI.Shell;
using Microsoft.Extensions.Logging;

namespace App.UI.Shared.Services;

/// <summary>
/// A funder investment request tracked by <see cref="FundersMonitor"/>.
/// Snapshot of the SDK Investment plus the wallet/project context needed by the UI.
/// </summary>
public record FunderRecord(
    string EventId,
    string WalletId,
    string ProjectIdentifier,
    string ProjectTitle,
    DateTime CreatedOn,
    string InvestmentTransactionHex,
    string InvestorNostrPubKey,
    long AmountSats,
    InvestmentStatus Status);

/// <summary>
/// Singleton background monitor for founder investment requests ("funders").
///
/// Why: the Funders tab used to scan Nostr relays only when activated, which was slow
/// and meant new requests (and post-approval funding) went unnoticed. This monitor:
///  - polls all founder projects for investment handshakes on a timer,
///  - keeps a warm snapshot so the Funders tab renders instantly,
///  - detects new pending requests and "approved investor has now funded" transitions,
///  - exposes a pending count for the shell badge and raises toast notifications.
///
/// Thread safety: RefreshAsync runs on background threads; Updated/NotificationRaised
/// are marshalled to the UI thread via Dispatcher.UIThread.Post (see repo pitfalls).
/// </summary>
public sealed class FundersMonitor : IDisposable
{
    /// <summary>Baseline poll interval. Each poll re-sends REQ filters on the already-open
    /// relay websockets (EOSE usually within a few seconds), so polling is cheap; the
    /// refresh lock coalesces overlaps if a sync ever runs longer than the interval.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    /// <summary>Faster cadence while auto-approve is on — an investor is actively waiting
    /// for the founder's signatures, so keep their wait short.</summary>
    private static readonly TimeSpan AutoApprovePollInterval = TimeSpan.FromSeconds(30);

    private readonly IFounderAppService _founderAppService;
    private readonly IProjectAppService _projectAppService;
    private readonly IWalletContext _walletContext;
    private readonly PrototypeSettings _settings;
    private readonly ILogger<FundersMonitor> _logger;

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly CompositeDisposable _disposables = new();
    private readonly object _stateLock = new();

    /// <summary>Statuses from the previous poll, keyed by EventId. Null until first completed poll.</summary>
    private Dictionary<string, InvestmentStatus>? _previousStatuses;

    /// <summary>EventIds the founder approved locally this session (optimistic overlay until the
    /// approval DM round-trips through Nostr and the synced status catches up).</summary>
    private readonly HashSet<string> _locallyApproved = new();

    private IReadOnlyList<FunderRecord> _snapshot = Array.Empty<FunderRecord>();
    private CancellationTokenSource? _loopCts;
    private bool _started;

    /// <summary>Raised on the UI thread after every completed refresh.</summary>
    public event Action? Updated;

    /// <summary>Raised on the UI thread with a human-readable message when something new arrives
    /// (new pending request, or an approved investor's funding transaction landed on-chain).</summary>
    public event Action<string>? NotificationRaised;

    /// <summary>True once at least one poll completed (snapshot is meaningful).</summary>
    public bool HasLoaded { get; private set; }

    /// <summary>Latest funder records across all wallets/projects (immutable snapshot).</summary>
    public IReadOnlyList<FunderRecord> Snapshot
    {
        get { lock (_stateLock) return _snapshot; }
    }

    /// <summary>Count of requests awaiting founder approval — drives the shell badge.</summary>
    public int PendingCount
    {
        get { lock (_stateLock) return _snapshot.Count(r => EffectiveStatus(r) == InvestmentStatus.PendingFounderSignatures); }
    }

    public FundersMonitor(
        IFounderAppService founderAppService,
        IProjectAppService projectAppService,
        IWalletContext walletContext,
        PrototypeSettings settings,
        ILogger<FundersMonitor> logger)
    {
        _founderAppService = founderAppService;
        _projectAppService = projectAppService;
        _walletContext = walletContext;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Start the background polling loop. Idempotent. Also refreshes shortly after
    /// wallets change (throttled) so the first poll happens once wallets are loaded.
    /// </summary>
    public void Start()
    {
        if (_started) return;
        _started = true;

        _loopCts = new CancellationTokenSource();

        // Refresh when wallets load/change (throttled to avoid bursts during startup).
        _walletContext.WalletsUpdated
            .Throttle(TimeSpan.FromSeconds(3))
            .Subscribe(unit => { _ = RefreshAsync(); })
            .DisposeWith(_disposables);

        // When the founder switches auto-approve on, run a refresh immediately so any
        // requests already waiting get approved without waiting for the next poll.
        // The relay websockets stay open between polls (auto-reconnect after 30s if
        // dropped); each refresh just re-sends the REQ filters on the existing client.
        _settings.WhenAnyValue(x => x.IsAutoApproveEnabled)
            .DistinctUntilChanged()
            .Where(enabled => enabled)
            .Subscribe(enabled => { _ = RefreshAsync(); })
            .DisposeWith(_disposables);

        _ = Task.Run(() => PollLoopAsync(_loopCts.Token));
    }

    private async Task PollLoopAsync(CancellationToken token)
    {
        try
        {
            // Small startup delay so we don't compete with initial wallet/portfolio loads.
            await Task.Delay(TimeSpan.FromSeconds(8), token);
            while (!token.IsCancellationRequested)
            {
                await RefreshAsync();
                await Task.Delay(_settings.IsAutoApproveEnabled ? AutoApprovePollInterval : PollInterval, token);
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    /// <summary>
    /// Poll all founder projects for investment requests, update the snapshot,
    /// and raise notifications for anything new. Reentrancy-safe: concurrent calls
    /// coalesce onto the in-flight refresh.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (!await _refreshLock.WaitAsync(0))
        {
            // A refresh is already running — wait for it instead of stacking another.
            await _refreshLock.WaitAsync();
            _refreshLock.Release();
            return;
        }

        try
        {
            var wallets = _walletContext.Wallets.ToList();
            if (wallets.Count == 0) return;

            var records = new List<FunderRecord>();

            foreach (var wallet in wallets)
            {
                var projectsResult = await _projectAppService.GetFounderProjects(wallet.Id);
                if (projectsResult.IsFailure) continue;

                foreach (var project in projectsResult.Value.Projects)
                {
                    if (project.Id == null) continue;

                    var investmentsResult = await _founderAppService.GetProjectInvestments(
                        new GetProjectInvestments.GetProjectInvestmentsRequest(wallet.Id, project.Id));

                    if (investmentsResult.IsFailure)
                    {
                        _logger.LogWarning(
                            "FundersMonitor: GetProjectInvestments failed for project {ProjectId}: {Error}",
                            project.Id.Value, investmentsResult.Error);
                        continue;
                    }

                    foreach (var investment in investmentsResult.Value.Investments)
                    {
                        records.Add(new FunderRecord(
                            investment.EventId ?? "",
                            wallet.Id.Value,
                            project.Id.Value,
                            project.Name ?? "Unknown Project",
                            investment.CreatedOn,
                            investment.InvestmentTransactionHex ?? "",
                            investment.InvestorNostrPubKey ?? "",
                            investment.Amount,
                            investment.Status));
                    }
                }
            }

            // Cancel + reinvest with the same investor key produces multiple handshakes
            // for the same (project, investor) pair. Only the most recent one reflects
            // the investor's current request — older entries are superseded, so showing
            // them would duplicate the investor in the list and skew the pending badge.
            records = records
                .GroupBy(r => (r.WalletId, r.ProjectIdentifier, PubKey: r.InvestorNostrPubKey))
                .SelectMany(g => g.Key.PubKey.Length == 0
                    ? g.AsEnumerable() // no pubkey to correlate on — keep all
                    : new[] { g.OrderByDescending(r => r.CreatedOn).First() })
                .ToList();

            // ── Auto-approve: sign every newly discovered pending request ──
            // Signing is unattended: seed words resolve via the OS-secured key store,
            // so no password prompt is required. Runs before change-detection so the
            // "awaiting your approval" notification is replaced by "auto-approved".
            var autoApprovedIds = new HashSet<string>();
            var autoApproveMessages = new List<string>();
            if (_settings.IsAutoApproveEnabled)
            {
                List<FunderRecord> pending;
                lock (_stateLock)
                {
                    pending = records
                        .Where(r => r.Status == InvestmentStatus.PendingFounderSignatures
                                    && r.EventId.Length > 0
                                    && r.InvestmentTransactionHex.Length > 0
                                    && !_locallyApproved.Contains(r.EventId))
                        .GroupBy(r => r.EventId)
                        .Select(g => g.First())
                        .ToList();
                }

                foreach (var record in pending)
                {
                    if (await TryAutoApproveAsync(record))
                    {
                        autoApprovedIds.Add(record.EventId);
                        autoApproveMessages.Add(
                            $"Auto-approved funding request for \"{record.ProjectTitle}\" — {FormatBtc(record.AmountSats)}");
                    }
                }
            }

            List<string> notifications;
            lock (_stateLock)
            {
                notifications = DetectChanges(records, autoApprovedIds);
                _snapshot = records;
                // Overlay auto-approvals so the UI shows them as approved immediately.
                _locallyApproved.UnionWith(autoApprovedIds);
                // Drop local-approval overlays once the synced status caught up.
                _locallyApproved.RemoveWhere(id =>
                    records.Any(r => r.EventId == id && r.Status != InvestmentStatus.PendingFounderSignatures));
                _previousStatuses = records
                    .Where(r => r.EventId.Length > 0)
                    .GroupBy(r => r.EventId)
                    .ToDictionary(g => g.Key, g => g.First().Status);
                HasLoaded = true;
            }

            notifications.AddRange(autoApproveMessages);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Updated?.Invoke();
                foreach (var message in notifications)
                    NotificationRaised?.Invoke(message);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FundersMonitor refresh failed");
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Drop all state derived from the previous network (snapshot, change-detection
    /// baseline, optimistic overlays). Called by the shell on a network switch so
    /// stale funder records never survive into the new network; the next poll or
    /// explicit RefreshAsync rebuilds the snapshot from the new network's data.
    /// </summary>
    public void Reset()
    {
        lock (_stateLock)
        {
            _snapshot = Array.Empty<FunderRecord>();
            _previousStatuses = null;
            _locallyApproved.Clear();
            HasLoaded = false;
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(() => Updated?.Invoke());
    }

    /// <summary>
    /// Mark a request as approved locally (optimistic). The snapshot keeps reporting
    /// the synced status, but <see cref="EffectiveStatus"/> overlays this until the
    /// approval DM is visible on relays — prevents the badge/tab flipping back to
    /// "waiting" on the next poll.
    /// </summary>
    public void MarkApprovedLocally(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return;
        lock (_stateLock) _locallyApproved.Add(eventId);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Updated?.Invoke());
    }

    /// <summary>Status of a record with the local-approval overlay applied.</summary>
    public InvestmentStatus EffectiveStatus(FunderRecord record)
    {
        if (record.Status == InvestmentStatus.PendingFounderSignatures)
        {
            lock (_stateLock)
            {
                if (_locallyApproved.Contains(record.EventId))
                    return InvestmentStatus.FounderSignaturesReceived;
            }
        }

        return record.Status;
    }

    /// <summary>
    /// Approve a single pending request via the SDK (builds recovery signatures and
    /// publishes the encrypted DM to the investor). Returns true on success.
    /// </summary>
    private async Task<bool> TryAutoApproveAsync(FunderRecord record)
    {
        try
        {
            var investment = new Angor.Sdk.Funding.Founder.Domain.Investment(
                record.EventId,
                DateTime.UtcNow,
                record.InvestmentTransactionHex,
                record.InvestorNostrPubKey,
                record.AmountSats,
                InvestmentStatus.PendingFounderSignatures);

            var result = await _founderAppService.ApproveInvestment(
                new ApproveInvestment.ApproveInvestmentRequest(
                    new WalletId(record.WalletId),
                    new ProjectId(record.ProjectIdentifier),
                    investment));

            if (result.IsSuccess) return true;

            _logger.LogWarning(
                "FundersMonitor: auto-approve failed for request {EventId} in project {ProjectId}: {Error}",
                record.EventId, record.ProjectIdentifier, result.Error);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "FundersMonitor: auto-approve threw for request {EventId} in project {ProjectId}",
                record.EventId, record.ProjectIdentifier);
            return false;
        }
    }

    /// <summary>Compare against the previous poll and build notification messages.
    /// Requests in <paramref name="autoApprovedIds"/> are skipped — the caller raises
    /// an "auto-approved" message for them instead.
    /// Must be called under <see cref="_stateLock"/>.</summary>
    private List<string> DetectChanges(List<FunderRecord> records, HashSet<string> autoApprovedIds)
    {
        var notifications = new List<string>();

        // First poll establishes the baseline silently — we only notify about changes
        // that happen while the app is running.
        if (_previousStatuses == null) return notifications;

        foreach (var record in records)
        {
            if (record.EventId.Length == 0) continue;
            if (autoApprovedIds.Contains(record.EventId)) continue;

            if (!_previousStatuses.TryGetValue(record.EventId, out var previous))
            {
                switch (record.Status)
                {
                    case InvestmentStatus.PendingFounderSignatures:
                        notifications.Add(
                            $"New funding request for \"{record.ProjectTitle}\" — {FormatBtc(record.AmountSats)} awaiting your approval");
                        break;
                    case InvestmentStatus.Invested:
                        notifications.Add(
                            $"New investment in \"{record.ProjectTitle}\" — {FormatBtc(record.AmountSats)} received");
                        break;
                }

                continue;
            }

            // Approved (or pending) → funded: the investor broadcast after our approval.
            if (record.Status == InvestmentStatus.Invested && previous != InvestmentStatus.Invested)
            {
                notifications.Add(
                    $"Approved funder has invested {FormatBtc(record.AmountSats)} in \"{record.ProjectTitle}\"");
            }
        }

        return notifications;
    }

    private static string FormatBtc(long sats) =>
        (sats / 100_000_000m).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + " BTC";

    public void Dispose()
    {
        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _disposables.Dispose();
        _refreshLock.Dispose();
    }
}

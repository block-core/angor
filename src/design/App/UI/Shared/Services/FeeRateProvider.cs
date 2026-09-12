using System.Threading;
using Angor.Sdk.Wallet.Application;
using Microsoft.Extensions.Logging;

namespace App.UI.Shared.Services;

/// <summary>
/// The three fee-rate presets offered in the UI, in sat/vByte.
/// </summary>
public record FeeRates(long Priority, long Standard, long Economy)
{
    /// <summary>
    /// Used when the indexer is unreachable or returns nothing usable.
    /// These were the hardcoded values before live rates were wired up.
    /// </summary>
    public static readonly FeeRates Fallback = new(50, 20, 5);
}

/// <summary>
/// Supplies current network fee rates for the UI fee presets.
/// </summary>
public interface IFeeRateProvider
{
    /// <summary>
    /// Last known fee rates, without hitting the network. Returns
    /// <see cref="FeeRates.Fallback"/> until a fetch has succeeded.
    /// For callers that cannot await — see <see cref="Warm"/>.
    /// </summary>
    FeeRates Current { get; }

    /// <summary>
    /// Current fee rates in sat/vByte. Cached briefly; never throws and never
    /// blocks for long — returns <see cref="FeeRates.Fallback"/> if the indexer
    /// is slow or unavailable.
    /// </summary>
    Task<FeeRates> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Kicks off a refresh in the background so a later <see cref="Current"/> read
    /// has live values. Fire and forget; failures are logged, not surfaced.
    /// </summary>
    void Warm();
}

/// <summary>
/// Fetches fee rates from the indexer via <see cref="IWalletAppService.GetFeeEstimates"/>
/// and maps them onto the Priority / Standard / Economy presets.
///
/// Mirrors the mapping the Avalonia app does in UIServices.GetFeeratePresetsAsync:
/// the SDK returns FeeEstimation.FeeRate in sat/kB keyed by target confirmations
/// (1, 3, 6, 18 — see MempoolSpaceIndexerApi.GetFeeEstimationAsync).
/// </summary>
public class FeeRateProvider(
    IWalletAppService walletAppService,
    ILogger<FeeRateProvider> logger) : IFeeRateProvider
{
    /// <summary>How long a fetched set of rates stays fresh.</summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Upper bound on how long the UI will wait. Opening the deploy overlay or the
    /// fee popup must not hang on a slow indexer, so we fall back instead.
    /// </summary>
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(3);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private FeeRates? _cached;
    private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;

    public FeeRates Current => _cached ?? FeeRates.Fallback;

    public void Warm()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await GetAsync();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Background fee rate warm-up failed");
            }
        });
    }

    public async Task<FeeRates> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cached != null && DateTimeOffset.UtcNow - _cachedAt < CacheDuration)
        {
            return _cached;
        }

        if (!await _gate.WaitAsync(FetchTimeout, cancellationToken))
        {
            // Another fetch is in flight and taking too long — don't stack up on it.
            return _cached ?? FeeRates.Fallback;
        }

        try
        {
            // Re-check: another caller may have refreshed while we waited on the gate.
            if (_cached != null && DateTimeOffset.UtcNow - _cachedAt < CacheDuration)
            {
                return _cached;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(FetchTimeout);

            var result = await walletAppService.GetFeeEstimates().WaitAsync(timeout.Token);

            if (result.IsFailure)
            {
                logger.LogWarning("Fee estimate fetch failed, using fallback rates: {Error}", result.Error);
                return _cached ?? FeeRates.Fallback;
            }

            var rates = Map(result.Value);
            _cached = rates;
            _cachedAt = DateTimeOffset.UtcNow;

            logger.LogInformation(
                "Fee rates from indexer (sat/vB): priority={Priority}, standard={Standard}, economy={Economy}",
                rates.Priority, rates.Standard, rates.Economy);

            return rates;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Fee estimate fetch timed out after {Timeout}s, using fallback rates",
                FetchTimeout.TotalSeconds);
            return _cached ?? FeeRates.Fallback;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fee estimate fetch threw, using fallback rates");
            return _cached ?? FeeRates.Fallback;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Maps SDK fee estimates (sat/kB, keyed by target confirmations) onto the three
    /// UI presets (sat/vByte). Picks the closest available estimate for each target and
    /// clamps to at least 1 sat/vB so we never build a sub-relay-minimum transaction.
    /// </summary>
    public static FeeRates Map(IEnumerable<Angor.Shared.Models.FeeEstimation> estimates)
    {
        var usable = estimates?
            .Where(e => e.FeeRate > 0)
            .ToList();

        if (usable == null || usable.Count == 0)
        {
            return FeeRates.Fallback;
        }

        long ForTarget(int confirmations)
        {
            var closest = usable
                .OrderBy(e => Math.Abs(e.Confirmations - confirmations))
                .ThenBy(e => e.Confirmations)
                .First();

            return Math.Max(1, closest.FeeRate / 1000); // sat/kB -> sat/vByte
        }

        return new FeeRates(
            Priority: ForTarget(1),
            Standard: ForTarget(3),
            Economy: ForTarget(18));
    }
}

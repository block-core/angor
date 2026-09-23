using System.Collections.Concurrent;
using Angor.Shared.Networks;

namespace Angor.Shared.Integration.Lightning.Models
{
    /// <summary>
    /// Configuration for Boltz-compatible swap services.
    /// The active base URL is resolved per-call via <see cref="ResolveBaseUrl"/>,
    /// so a runtime network switch (mainnet ↔ testnet) is honoured without rebuilding the container.
    ///
    /// Since Boltz disabled swaps on their hosted mainnet API (see swapmarket.github.io),
    /// mainnet supports an ordered list of Boltz-v2-compatible backends. The first backend
    /// that responds successfully is "pinned" per network so that all subsequent calls for
    /// a swap (status, claim, websocket) hit the same provider that created it.
    /// </summary>
    public class BoltzConfiguration
    {
        /// <summary>
        /// Ordered list of Boltz-v2-compatible mainnet backends (from swapmarket.github.io).
        /// SATS Routing is first because Boltz disabled swap creation on their hosted API;
        /// Boltz remains as a fallback in case they re-enable swaps.
        /// </summary>
        public static readonly string[] MainnetUrls =
        {
            "https://satsrouting.exchange",
            "https://api.boltz.exchange"
        };

        public const string MainnetUrl = "https://satsrouting.exchange";
        public const string TestnetUrl = "https://test.boltz.angor.io/";

        /// <summary>Pinned base URL per network name (set after a successful create/fees call).</summary>
        private readonly ConcurrentDictionary<string, string> _pinnedBaseUrls = new();

        /// <summary>
        /// Optional explicit base URL override. When non-empty, takes precedence over the
        /// network-derived URL (used for integration tests and the BOLTZ_API_URL env var).
        /// When null/empty, services pick from <see cref="MainnetUrls"/> or <see cref="TestnetUrl"/>
        /// based on the current <see cref="INetworkConfiguration"/>.
        /// </summary>
        public string? OverrideBaseUrl { get; set; }

        /// <summary>
        /// Request timeout in seconds.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Whether to use /v2/ prefix for API endpoints.
        /// Mainnet API uses no prefix (endpoints like /swap/reverse).
        /// Some local/test instances may require /v2/ prefix.
        /// </summary>
        public bool UseV2Prefix { get; set; } = true;

        /// <summary>
        /// Gets the API path prefix based on configuration.
        /// </summary>
        public string ApiPrefix => UseV2Prefix ? "v2/" : "";

        /// <summary>
        /// Resolves the active base URL. Uses <see cref="OverrideBaseUrl"/> when set,
        /// then the pinned backend for the current network (if a previous call succeeded),
        /// otherwise the first candidate for the current network (mainnet vs. test/signet).
        /// Always returns a value ending in '/' so it can be safely used as an
        /// <see cref="Uri"/> base for relative requests.
        /// </summary>
        public string ResolveBaseUrl(INetworkConfiguration networkConfiguration)
        {
            if (!string.IsNullOrWhiteSpace(OverrideBaseUrl))
                return Normalize(OverrideBaseUrl!);

            var network = networkConfiguration.GetNetwork();
            if (_pinnedBaseUrls.TryGetValue(network.Name, out var pinned))
                return Normalize(pinned);

            return Normalize(network.IsMainnet ? MainnetUrls[0] : TestnetUrl);
        }

        /// <summary>
        /// Returns the ordered candidate base URLs for the current network,
        /// with any pinned backend first. A single-element list when an
        /// override is set or on testnet.
        /// </summary>
        public IReadOnlyList<string> GetCandidateBaseUrls(INetworkConfiguration networkConfiguration)
        {
            if (!string.IsNullOrWhiteSpace(OverrideBaseUrl))
                return new[] { Normalize(OverrideBaseUrl!) };

            var network = networkConfiguration.GetNetwork();
            if (!network.IsMainnet)
                return new[] { Normalize(TestnetUrl) };

            var candidates = new List<string>(MainnetUrls.Length);
            if (_pinnedBaseUrls.TryGetValue(network.Name, out var pinned))
                candidates.Add(Normalize(pinned));

            foreach (var url in MainnetUrls)
            {
                var normalized = Normalize(url);
                if (!candidates.Contains(normalized))
                    candidates.Add(normalized);
            }

            return candidates;
        }

        /// <summary>
        /// Pins a backend for the current network so subsequent swap-scoped calls
        /// (status, claim, websocket) hit the same provider that created the swap.
        /// </summary>
        public void PinBaseUrl(INetworkConfiguration networkConfiguration, string baseUrl)
        {
            var network = networkConfiguration.GetNetwork();
            _pinnedBaseUrls[network.Name] = Normalize(baseUrl);
        }

        private static string Normalize(string url) => url.EndsWith('/') ? url : url + "/";
    }
}

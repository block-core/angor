using Blockcore.Networks;

namespace Angor.Shared.Integration.Lightning.Models
{
    /// <summary>
    /// Configuration for Boltz swap service.
    /// The active base URL is resolved per-call via <see cref="ResolveBaseUrl"/>,
    /// so a runtime network switch (mainnet ↔ testnet) is honoured without rebuilding the container.
    /// </summary>
    public class BoltzConfiguration
    {
        public const string MainnetUrl = "https://api.boltz.exchange";
        public const string TestnetUrl = "https://test.boltz.angor.io/";

        /// <summary>
        /// Optional explicit base URL override. When non-empty, takes precedence over the
        /// network-derived URL (used for integration tests and the BOLTZ_API_URL env var).
        /// When null/empty, services pick <see cref="MainnetUrl"/> or <see cref="TestnetUrl"/>
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
        /// otherwise picks the URL for the current network (mainnet vs. test/signet).
        /// Always returns a value ending in '/' so it can be safely used as an
        /// <see cref="Uri"/> base for relative requests.
        /// </summary>
        public string ResolveBaseUrl(INetworkConfiguration networkConfiguration)
        {
            var resolved = !string.IsNullOrWhiteSpace(OverrideBaseUrl)
                ? OverrideBaseUrl!
                : SelectByNetwork(networkConfiguration);

            return resolved.EndsWith('/') ? resolved : resolved + "/";
        }

        private static string SelectByNetwork(INetworkConfiguration networkConfiguration)
        {
            var network = networkConfiguration.GetNetwork();
            return network.NetworkType == NetworkType.Mainnet ? MainnetUrl : TestnetUrl;
        }
    }
}

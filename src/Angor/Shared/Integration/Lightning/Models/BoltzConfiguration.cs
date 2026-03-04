<<<<<<<< HEAD:src/Angor/Shared/Integration/Lightning/Models/BoltzConfiguration.cs
namespace Angor.Shared.Integration.Lightning.Models
{
    /// <summary>
    /// Configuration for Boltz swap service.
    /// Set BaseUrl based on your environment (mainnet or testnet).
    /// </summary>
    public class BoltzConfiguration
    {
        public const string MainnetUrl = "https://api.boltz.exchange";
        public const string TestnetUrl = "https://boltz.thedude.cloud/"; //todo move to angor domain

        /// <summary>
        /// The Boltz API base URL. Defaults to mainnet.
        /// </summary>
        public string BaseUrl { get; set; } = MainnetUrl;

        /// <summary>
        /// Request timeout in seconds.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Whether to use /v2/ prefix for API endpoints.
        /// Mainnet API uses no prefix (endpoints like /swap/reverse).
        /// Some local/test instances may require /v2/ prefix.
        /// </summary>
        public bool UseV2Prefix { get; set; } = false;

        /// <summary>
        /// Gets the API path prefix based on configuration.
        /// </summary>
        public string ApiPrefix => UseV2Prefix ? "v2/" : "";
    }
}

========
// Moved to Angor.Shared.Integration.Lightning.Models — see LightningGlobalUsings.cs
>>>>>>>> 2f47cc51 (Refactor Boltz integration: move models and services to Angor.Shared.Integration.Lightning namespace):src/Angor/Avalonia/Angor.Sdk/Integration/Lightning/Models/BoltzConfiguration.cs

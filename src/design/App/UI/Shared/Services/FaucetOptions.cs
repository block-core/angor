namespace App.UI.Shared.Services;

/// <summary>
/// Faucet endpoint configuration. <see cref="SendPathTemplate"/> is a <see cref="string.Format(string, object?, object?)"/>
/// template where <c>{0}</c> is the destination address and <c>{1}</c> is the amount in BTC.
/// </summary>
/// <remarks>
/// Production (angor.io) uses <c>api/faucet/send/{0}/{1}</c>.
/// The local docker stack (<c>block-core/bitcoin-custom-signet</c>) uses <c>api/send/{0}/{1}</c>.
/// </remarks>
public sealed record FaucetOptions(string BaseUrl, string SendPathTemplate)
{
    public static FaucetOptions AngorPublic { get; } = new(
        "https://faucettmp.angor.io",
        "api/faucet/send/{0}/{1}");
}

using System.Globalization;
using System.Web;

namespace Angor.Client.Services;

public enum NetworkHandoffAction
{
    /// <summary>No network requested, an invalid one, or the requested network is already active.</summary>
    None,

    /// <summary>The requested network differs and the browser has no wallet, so it is safe to switch.</summary>
    Switch,

    /// <summary>The requested network differs and a wallet exists on the current network.</summary>
    Reject
}

public readonly record struct NetworkHandoffDecision(NetworkHandoffAction Action, string? RequestedNetwork);

/// <summary>
/// Pure rules for query-string values handed over by the Hub (/invest/{id}?network=...&amp;amount=...).
/// </summary>
public static class InvestHandoff
{
    private static readonly string[] SupportedNetworks = { "Main", "Angornet" };

    public static NetworkHandoffDecision EvaluateNetwork(string uri, string currentNetworkName, bool hasWallet)
    {
        string? requested = HttpUtility.ParseQueryString(new Uri(uri).Query)["network"];
        if (requested == null || !SupportedNetworks.Contains(requested) || requested == currentNetworkName)
            return new NetworkHandoffDecision(NetworkHandoffAction.None, null);

        return new NetworkHandoffDecision(
            hasWallet ? NetworkHandoffAction.Reject : NetworkHandoffAction.Switch,
            requested);
    }

    /// <summary>
    /// Parses ?amount= with the invariant culture. Returns null when the value is missing, malformed,
    /// zero or negative, or when the selected pattern already fixes the amount.
    /// </summary>
    public static decimal? ParseRequestedAmount(string? raw, bool patternHasFixedAmount)
    {
        if (patternHasFixedAmount)
            return null;

        if (!decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount))
            return null;

        return amount > 0 ? amount : null;
    }
}

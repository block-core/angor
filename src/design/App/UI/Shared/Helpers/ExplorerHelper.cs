using Angor.Shared.Services;

namespace App.UI.Shared.Helpers;

/// <summary>
/// Builds indexer-hosted URLs using the primary indexer (mempool-style)
/// configured in <see cref="INetworkService"/>, and opens them in the default browser.
/// </summary>
public static class ExplorerHelper
{
    /// <summary>
    /// Build a full indexer URL for the given transaction ID.
    /// Returns null if the txid is empty or no indexer is configured.
    /// </summary>
    public static string? GetTransactionUrl(INetworkService networkService, string? txid)
    {
        if (string.IsNullOrEmpty(txid)) return null;
        try
        {
            var indexer = networkService.GetPrimaryIndexer();
            return $"{indexer.Url.TrimEnd('/')}/tx/{txid}";
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Build a full indexer URL for the given address.
    /// Returns null if the address is empty or no indexer is configured.
    /// </summary>
    public static string? GetAddressUrl(INetworkService networkService, string? address)
    {
        if (string.IsNullOrEmpty(address)) return null;
        try
        {
            var indexer = networkService.GetPrimaryIndexer();
            return $"{indexer.Url.TrimEnd('/')}/address/{address}";
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Open a transaction page from the primary indexer in the system browser.
    /// No-ops silently if the txid is empty or the browser cannot be launched.
    /// </summary>
    public static void OpenTransaction(INetworkService networkService, string? txid)
    {
        var url = GetTransactionUrl(networkService, txid);
        if (url == null) return;
        OpenUrl(url);
    }

    /// <summary>
    /// Open an address page from the primary indexer in the system browser.
    /// No-ops silently if the address is empty or the browser cannot be launched.
    /// </summary>
    public static void OpenAddress(INetworkService networkService, string? address)
    {
        var url = GetAddressUrl(networkService, address);
        if (url == null) return;
        OpenUrl(url);
    }

    /// <summary>
    /// Open an arbitrary URL in the system's default browser.
    /// </summary>
    public static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch
        {
            // Silently fail if browser can't be opened
        }
    }
}

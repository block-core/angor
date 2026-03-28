using Angor.Shared.Services;

namespace App.UI.Shared.Helpers;

/// <summary>
/// Builds block-explorer URLs from transaction IDs using the primary explorer
/// configured in <see cref="INetworkService"/>, and opens them in the default browser.
/// </summary>
public static class ExplorerHelper
{
    /// <summary>
    /// Build a full explorer URL for the given transaction ID.
    /// Returns null if the txid is empty or no explorer is configured.
    /// </summary>
    public static string? GetTransactionUrl(INetworkService networkService, string? txid)
    {
        if (string.IsNullOrEmpty(txid)) return null;
        try
        {
            var explorer = networkService.GetPrimaryExplorer();
            return $"{explorer.Url.TrimEnd('/')}/tx/{txid}";
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Open a transaction in the system's default browser.
    /// No-ops silently if the txid is empty or the browser cannot be launched.
    /// </summary>
    public static void OpenTransaction(INetworkService networkService, string? txid)
    {
        var url = GetTransactionUrl(networkService, txid);
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

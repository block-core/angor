using Angor.Sdk.WalletExport;
using Angor.Sdk.Common;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace App.UI.Sections.Settings;

/// <summary>
/// Cloud-backup section of the Settings page.
/// Wraps <see cref="IWalletCloudBackupService"/> and <see cref="IBackupRecoveryService"/> into the
/// existing settings UI. The recovery passphrase is buffered as <c>string</c> here purely because
/// Avalonia text-box bindings work on strings; the actual key derivation in <see cref="BackupKeys"/>
/// re-encodes to UTF-8 bytes and zeroes them immediately after Argon2id.
/// </summary>
public partial class SettingsViewModel
{
    // ── Reactive state ──
    [Reactive] private bool isCloudBackupEnabled;
    [Reactive] private string? cloudBackupBlobSha256;
    [Reactive] private int cloudBackupServersHealthy;
    [Reactive] private int cloudBackupServersTotal;
    [Reactive] private long? cloudBackupLastVerifiedUnix;
    [Reactive] private bool isCloudBackupBusy;
    [Reactive] private string? cloudBackupStatusMessage;

    // ── Modal state ──
    [Reactive] private bool isEnableBackupModalOpen;
    [Reactive] private string enablePassphrase = string.Empty;
    [Reactive] private string enablePassphraseConfirm = string.Empty;
    [Reactive] private string enableLabel = string.Empty;
    [Reactive] private bool enableHasWrittenItDown;
    [Reactive] private bool enableUnderstandsRisk;
    [Reactive] private bool enableUnderstandsDistinctFromWalletPassword;

    [Reactive] private bool isRefreshBackupModalOpen;
    [Reactive] private string refreshPassphrase = string.Empty;

    /// <summary>
    /// True when the Enable wizard's Continue button should be active.
    /// </summary>
    public bool CanContinueEnable =>
        !IsCloudBackupBusy
        && !string.IsNullOrWhiteSpace(EnablePassphrase)
        && EnablePassphrase.Length >= 12
        && EnablePassphrase == EnablePassphraseConfirm
        && EnableHasWrittenItDown
        && EnableUnderstandsRisk
        && EnableUnderstandsDistinctFromWalletPassword;

    /// <summary>
    /// Load current backup state from the SDK. Safe to call any time.
    /// </summary>
    public async Task RefreshCloudBackupStatusAsync()
    {
        var activeWallet = _walletContext.SelectedWallet;
        if (activeWallet is null)
        {
            IsCloudBackupEnabled = false;
            return;
        }

        var walletId = activeWallet.Id;
        var statusResult = await _cloudBackupService.GetStatus(walletId);
        if (statusResult.IsFailure)
        {
            _logger.LogWarning("Failed to load cloud backup status: {Error}", statusResult.Error);
            return;
        }

        if (statusResult.Value.HasNoValue)
        {
            IsCloudBackupEnabled = false;
            CloudBackupBlobSha256 = null;
            CloudBackupServersHealthy = 0;
            CloudBackupServersTotal = 0;
            CloudBackupLastVerifiedUnix = null;
            return;
        }

        var record = statusResult.Value.Value;
        IsCloudBackupEnabled = true;
        CloudBackupBlobSha256 = record.BlobSha256;
        CloudBackupServersTotal = record.Servers.Count;
        CloudBackupServersHealthy = record.ServerHealth.Count(kv => kv.Value);
        CloudBackupLastVerifiedUnix = record.LastVerifiedAtUnix;
    }

    public void OpenEnableBackupModal()
    {
        EnablePassphrase = string.Empty;
        EnablePassphraseConfirm = string.Empty;
        EnableLabel = string.Empty;
        EnableHasWrittenItDown = false;
        EnableUnderstandsRisk = false;
        EnableUnderstandsDistinctFromWalletPassword = false;
        CloudBackupStatusMessage = null;
        IsEnableBackupModalOpen = true;
    }

    public void CloseEnableBackupModal()
    {
        IsEnableBackupModalOpen = false;
        EnablePassphrase = string.Empty;
        EnablePassphraseConfirm = string.Empty;
    }

    public async Task ConfirmEnableBackupAsync()
    {
        if (!CanContinueEnable)
            return;

        var activeWallet = _walletContext.SelectedWallet;
        if (activeWallet is null)
        {
            ToastRequested?.Invoke("No wallet selected.");
            return;
        }

        IsCloudBackupBusy = true;
        CloudBackupStatusMessage = "Encrypting seed and uploading to Blossom servers…";

        try
        {
            var walletId = activeWallet.Id;
            var result = await _cloudBackupService.EnableAsync(walletId, EnablePassphrase, EnableLabel);
            if (result.IsFailure)
            {
                _logger.LogWarning("Cloud backup enable failed: {Error}", result.Error);
                CloudBackupStatusMessage = $"Backup failed: {result.Error}";
                ToastRequested?.Invoke("Backup failed.");
                return;
            }

            var created = result.Value;
            CloudBackupStatusMessage = $"Backup live on {created.UploadedToServers.Count} servers.";
            ToastRequested?.Invoke($"Cloud backup enabled ({created.UploadedToServers.Count} servers).");
            IsEnableBackupModalOpen = false;
            await RefreshCloudBackupStatusAsync();
        }
        finally
        {
            // Zero passphrase buffers immediately
            EnablePassphrase = string.Empty;
            EnablePassphraseConfirm = string.Empty;
            IsCloudBackupBusy = false;
        }
    }

    public async Task DisableBackupAsync()
    {
        var activeWallet = _walletContext.SelectedWallet;
        if (activeWallet is null) return;

        IsCloudBackupBusy = true;
        try
        {
            var walletId = activeWallet.Id;
            var result = await _cloudBackupService.DisableAsync(walletId);
            if (result.IsFailure)
            {
                ToastRequested?.Invoke($"Failed to disable backup: {result.Error}");
                return;
            }

            ToastRequested?.Invoke("Cloud backup disabled locally. The blob will be pruned naturally.");
            await RefreshCloudBackupStatusAsync();
        }
        finally
        {
            IsCloudBackupBusy = false;
        }
    }

    public async Task VerifyBackupHealthAsync()
    {
        var activeWallet = _walletContext.SelectedWallet;
        if (activeWallet is null) return;

        IsCloudBackupBusy = true;
        CloudBackupStatusMessage = "Checking blob availability…";
        try
        {
            var walletId = activeWallet.Id;
            var result = await _cloudBackupService.VerifyHealthAsync(walletId);
            if (result.IsFailure)
            {
                CloudBackupStatusMessage = $"Health check failed: {result.Error}";
                return;
            }
            CloudBackupStatusMessage = $"Reachable on {result.Value.ServersReachable} of {result.Value.ServersChecked} servers.";
            await RefreshCloudBackupStatusAsync();
        }
        finally
        {
            IsCloudBackupBusy = false;
        }
    }

    public void OpenRefreshBackupModal()
    {
        RefreshPassphrase = string.Empty;
        IsRefreshBackupModalOpen = true;
    }

    public void CloseRefreshBackupModal()
    {
        IsRefreshBackupModalOpen = false;
        RefreshPassphrase = string.Empty;
    }

    public async Task ConfirmRefreshBackupAsync()
    {
        var activeWallet = _walletContext.SelectedWallet;
        if (activeWallet is null) return;

        if (string.IsNullOrWhiteSpace(RefreshPassphrase))
        {
            ToastRequested?.Invoke("Recovery passphrase is required.");
            return;
        }

        IsCloudBackupBusy = true;
        CloudBackupStatusMessage = "Re-uploading and re-publishing…";
        try
        {
            var walletId = activeWallet.Id;
            var result = await _cloudBackupService.RefreshAsync(walletId, RefreshPassphrase);
            if (result.IsFailure)
            {
                CloudBackupStatusMessage = $"Refresh failed: {result.Error}";
                ToastRequested?.Invoke("Refresh failed — passphrase may be incorrect.");
                return;
            }

            CloudBackupStatusMessage = $"Refreshed across {result.Value.UploadedToServers.Count} servers.";
            ToastRequested?.Invoke("Backup refreshed.");
            IsRefreshBackupModalOpen = false;
            await RefreshCloudBackupStatusAsync();
        }
        finally
        {
            RefreshPassphrase = string.Empty;
            IsCloudBackupBusy = false;
        }
    }

    /// <summary>
    /// Preview a recovery attempt — fetches the manifest + blob, decrypts the seed, but does not
    /// import it into a new wallet. Used to let the user confirm they will be able to recover before
    /// they commit to a destructive action (wipe / fresh install).
    /// </summary>
    public async Task<Result<BackupRecoveryResult>> PreviewRecoveryAsync(string recoveryPassphrase)
    {
        return await _backupRecoveryService.RecoverAsync(recoveryPassphrase);
    }
}

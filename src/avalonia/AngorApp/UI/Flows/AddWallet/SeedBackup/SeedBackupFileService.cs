using System.IO;
using Avalonia.Platform.Storage;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.UI.Flows.AddWallet.SeedBackup;

public class SeedBackupFileService(UIServices uiServices, IStorageProvider storageProvider) : ISeedBackupFileService
{
    public async Task Save(string seedwords)
    {
        if (!storageProvider.CanSave)
        {
            await ShowDownloadError("Could not access the file picker.");
            return;
        }

        var file = await TryPickBackupFile();
        if (file is null)
        {
            return;
        }

        await SaveBackupFile(file, seedwords);
    }

    private async Task<IStorageFile?> TryPickBackupFile()
    {
        try
        {
            return await storageProvider.SaveFilePickerAsync(SeedBackupSaveOptions());
        }
        catch (Exception e)
        {
            await ShowDownloadError($"Could not open the file picker: {e.Message}");
            return null;
        }
    }

    private static FilePickerSaveOptions SeedBackupSaveOptions()
    {
        return new FilePickerSaveOptions
        {
            Title = "Save seed words backup",
            SuggestedFileName = "angor-seed-backup.txt",
            DefaultExtension = "txt",
            FileTypeChoices =
            [
                new FilePickerFileType("Text document")
                {
                    Patterns = ["*.txt"],
                    MimeTypes = ["text/plain"]
                }
            ]
        };
    }

    private async Task SaveBackupFile(IStorageFile file, string seedwords)
    {
        try
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(BackupContent(seedwords));
            await writer.FlushAsync();
            await uiServices.NotificationService.Show("Seed backup saved.", "Wallet");
        }
        catch (Exception e)
        {
            await ShowDownloadError($"Could not save the seed backup: {e.Message}");
        }
    }

    private Task ShowDownloadError(string message)
    {
        return uiServices.Dialog.ShowMessage("Download failed", message);
    }

    private static string BackupContent(string seedwords)
    {
        return $"Angor Wallet Seed Backup{Environment.NewLine}{Environment.NewLine}" +
               $"Created (UTC): {DateTimeOffset.UtcNow:O}{Environment.NewLine}{Environment.NewLine}" +
               $"Seed words:{Environment.NewLine}{seedwords}{Environment.NewLine}{Environment.NewLine}" +
               "Keep this file offline and never share it.";
    }
}

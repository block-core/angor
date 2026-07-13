using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using App.UI.Shared.Helpers;
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace App.UI.Shared.Controls;

/// <summary>
/// Log Viewer Modal — displays the latest application log file content
/// in a scrollable monospace text view. Opened from Settings > Export Logs section.
/// </summary>
public partial class LogViewerModal : UserControl, IBackdropCloseable
{
    private readonly ILogger<LogViewerModal> _logger;
    private string? _logFilePath;

    /// <summary>
    /// Parameterless constructor for XAML designer/loader.
    /// </summary>
    public LogViewerModal()
    {
        _logger = App.Services.GetRequiredService<ILoggerFactory>().CreateLogger<LogViewerModal>();

        InitializeComponent();

        var closeBtn = this.FindControl<Button>("CloseButton");
        if (closeBtn != null) closeBtn.Click += OnCloseClick;

        var refreshBtn = this.FindControl<Button>("RefreshButton");
        if (refreshBtn != null) refreshBtn.Click += OnRefreshClick;

        var copyBtn = this.FindControl<Button>("CopyLogsButton");
        if (copyBtn != null) copyBtn.Click += OnCopyLogsClick;

        // Load logs on attach
        AttachedToVisualTree += (_, _) => LoadLatestLogs();
    }

    public void OnBackdropCloseRequested() { }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        var shellVm = this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;
        shellVm?.HideModal();
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        LoadLatestLogs();
    }

    private async void OnCopyLogsClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var logContent = this.FindControl<TextBlock>("LogContentText");
            var text = logContent?.Text;
            if (string.IsNullOrWhiteSpace(text)) return;

            ClipboardHelper.CopyToClipboard(this, text);

            // Brief visual feedback
            var copyText = this.FindControl<TextBlock>("CopyButtonText");
            if (copyText != null)
            {
                copyText.Text = "Copied!";
                await Task.Delay(2000);
                copyText.Text = "Copy";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to copy logs to clipboard");
        }
    }

    private void LoadLatestLogs()
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                localAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }

            if (string.IsNullOrWhiteSpace(localAppData))
            {
                SetContent("Cannot determine application data directory.", null);
                return;
            }

            var logsDir = Path.Combine(localAppData, "Angor", "logs");

            if (!Directory.Exists(logsDir))
            {
                SetContent("No log directory found.", null);
                return;
            }

            var logFiles = Directory.GetFiles(logsDir, "*.log");
            if (logFiles.Length == 0)
            {
                SetContent("No log files found.", logsDir);
                return;
            }

            // Pick the most recently modified log file
            var latestLog = logFiles
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .First();

            _logFilePath = latestLog.FullName;

            // Read the tail of the file (last ~200KB to keep UI responsive)
            // Open with ReadWrite share since Serilog holds the file open
            using var fileStream = new FileStream(
                latestLog.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            const long maxBytes = 200 * 1024;
            if (fileStream.Length > maxBytes)
            {
                fileStream.Seek(-maxBytes, SeekOrigin.End);
                // Skip partial line
                using var reader = new StreamReader(fileStream);
                reader.ReadLine(); // discard partial first line
                var content = reader.ReadToEnd();
                SetContent($"... (showing last ~200KB of {latestLog.Name})\n\n{content}", latestLog.FullName);
            }
            else
            {
                using var reader = new StreamReader(fileStream);
                var content = reader.ReadToEnd();
                SetContent(content, latestLog.FullName);
            }

            // Scroll to bottom
            var scrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer");
            scrollViewer?.ScrollToEnd();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load logs");
            SetContent($"Failed to load logs: {ex.Message}", null);
        }
    }

    private void SetContent(string text, string? filePath)
    {
        var logContent = this.FindControl<TextBlock>("LogContentText");
        if (logContent != null)
        {
            logContent.Text = string.IsNullOrWhiteSpace(text) ? "(empty)" : text;
        }

        var pathText = this.FindControl<TextBlock>("LogFilePathText");
        if (pathText != null)
        {
            pathText.Text = filePath ?? "";
        }
    }
}

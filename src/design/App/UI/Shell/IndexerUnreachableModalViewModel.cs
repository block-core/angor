using System.Collections.ObjectModel;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Microsoft.Extensions.Logging;

namespace App.UI.Shell;

/// <summary>
/// Backs the "Indexer unreachable" popup shown by <see cref="ShellViewModel"/> when
/// the currently selected primary indexer fails to respond to a request. Deliberately
/// does NOT auto-fallback to another indexer — the user must explicitly pick one (or
/// add a custom URL), matching the "direct to popup" design decided for issue #971.
/// </summary>
public partial class IndexerUnreachableModalViewModel : ReactiveObject
{
    private readonly INetworkStorage _networkStorage;
    private readonly INetworkService _networkService;
    private readonly ILogger _logger;
    private readonly Action _closeModal;

    public ObservableCollection<IndexerPickerItem> Indexers { get; } = new();

    [Reactive] private string reason = "";
    [Reactive] private string newIndexerUrl = "";
    [Reactive] private bool isChecking;
    [Reactive] private string? statusMessage;

    public IndexerUnreachableModalViewModel(
        INetworkStorage networkStorage,
        INetworkService networkService,
        ILogger logger,
        string unreachableIndexerUrl,
        string failureReason,
        Action closeModal)
    {
        _networkStorage = networkStorage;
        _networkService = networkService;
        _logger = logger;
        _closeModal = closeModal;
        Reason = $"The indexer \"{unreachableIndexerUrl}\" isn't responding ({failureReason}). " +
                  "Choose a different indexer below to continue.";

        LoadIndexers();
    }

    private void LoadIndexers()
    {
        Indexers.Clear();
        var settings = _networkStorage.GetSettings();
        foreach (var indexer in settings.Indexers)
        {
            Indexers.Add(new IndexerPickerItem
            {
                Url = indexer.Url,
                IsPrimary = indexer.IsPrimary,
                Status = indexer.Status
            });
        }
    }

    /// <summary>
    /// Sets the given indexer as primary, persists it, and closes the modal.
    /// No automatic health-check/fallback loop — the user made an explicit choice.
    /// </summary>
    public void SelectIndexer(IndexerPickerItem item)
    {
        var settings = _networkStorage.GetSettings();
        foreach (var indexer in settings.Indexers)
        {
            indexer.IsPrimary = indexer.Url == item.Url;
        }

        _networkStorage.SetSettings(settings);
        StatusMessage = $"Now using indexer: {item.Url}";
        _closeModal();
    }

    public async Task AddAndSelectCustomIndexerAsync()
    {
        var url = NewIndexerUrl?.Trim();
        if (string.IsNullOrWhiteSpace(url))
            return;

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            StatusMessage = "Please enter a valid URL (e.g. https://example.com).";
            return;
        }

        IsChecking = true;
        try
        {
            var settings = _networkStorage.GetSettings();

            var existing = settings.Indexers.FirstOrDefault(i =>
                string.Equals(i.Url, url, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                existing = new SettingsUrl { Url = url, Status = UrlStatus.Unknown };
                settings.Indexers.Add(existing);
            }

            foreach (var indexer in settings.Indexers)
            {
                indexer.IsPrimary = indexer == existing;
            }

            _networkStorage.SetSettings(settings);
            NewIndexerUrl = "";
            StatusMessage = $"Now using indexer: {url}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add custom indexer {Url}", url);
            StatusMessage = "Failed to add that indexer. Please try again.";
        }
        finally
        {
            IsChecking = false;
        }

        _closeModal();
    }

    public void Close() => _closeModal();
}

/// <summary>Row item for the indexer picker list.</summary>
public class IndexerPickerItem
{
    public string Url { get; set; } = "";
    public bool IsPrimary { get; set; }
    public UrlStatus Status { get; set; }
    public string StatusLabel => Status == UrlStatus.Online ? "Online" : Status == UrlStatus.Offline ? "Offline" : "Unknown";
}

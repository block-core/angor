using System.IO;
using System.Reactive.Linq;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Sdk.Common;
using App.UI.Shared.Helpers;
using App.UI.Shared.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NBitcoin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace App.UI.Sections.MyProjects.Steps;

public partial class CreateProjectStep3View : UserControl
{
    private readonly ILogger<CreateProjectStep3View> _logger;
    private readonly BlossomUploadService _blossomService;

    private IDisposable? _bannerUrlSub;
    private IDisposable? _profileUrlSub;

    // Cached control references
    private TextBlock? _bannerStatusText;
    private TextBlock? _profileStatusText;
    private TextBox? _bannerBlossomServerTextBox;
    private TextBox? _profileBlossomServerTextBox;
    private Button? _bannerUploadBtn;
    private Button? _profileUploadBtn;

    public CreateProjectStep3View()
    {
        InitializeComponent();
        _logger = App.Services.GetRequiredService<ILoggerFactory>().CreateLogger<CreateProjectStep3View>();
        _blossomService = App.Services.GetRequiredService<BlossomUploadService>();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _bannerStatusText = this.FindControl<TextBlock>("BannerStatusText");
        _profileStatusText = this.FindControl<TextBlock>("ProfileStatusText");
        _bannerBlossomServerTextBox = this.FindControl<TextBox>("BannerBlossomServerTextBox");
        _profileBlossomServerTextBox = this.FindControl<TextBox>("ProfileBlossomServerTextBox");
        _bannerUploadBtn = this.FindControl<Button>("BannerUploadBtn");
        _profileUploadBtn = this.FindControl<Button>("ProfileUploadBtn");

        // Wire combined pick-and-upload buttons
        if (_bannerUploadBtn != null)
            _bannerUploadBtn.Click += (_, _) => _ = PickAndUploadAsync(true);
        if (_profileUploadBtn != null)
            _profileUploadBtn.Click += (_, _) => _ = PickAndUploadAsync(false);

        // Wire preview click-targets (banner panel + avatar circle) — same Pick & Upload flow
        WirePreviewClickTarget("BannerPreviewClickTarget", "BannerPreviewOverlay", isBanner: true);
        WirePreviewClickTarget("AvatarPreviewClickTarget", "AvatarPreviewOverlay", isBanner: false);

        // Watch ViewModel URL changes to update the preview
        if (DataContext is CreateProjectViewModel vm)
        {
            _bannerUrlSub = vm.WhenAnyValue(x => x.BannerUrl)
                .Throttle(TimeSpan.FromMilliseconds(400))
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(url => UpdatePreviewFromUrl(url, true));

            _profileUrlSub = vm.WhenAnyValue(x => x.ProfileUrl)
                .Throttle(TimeSpan.FromMilliseconds(400))
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(url => UpdatePreviewFromUrl(url, false));

            // Show current preview if URLs are already set (e.g. debug prefill)
            if (!string.IsNullOrWhiteSpace(vm.BannerUrl))
                UpdatePreviewFromUrl(vm.BannerUrl, true);
            if (!string.IsNullOrWhiteSpace(vm.ProfileUrl))
                UpdatePreviewFromUrl(vm.ProfileUrl, false);
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        _bannerUrlSub?.Dispose();
        _profileUrlSub?.Dispose();
        base.OnUnloaded(e);
    }

    #region Preview

    private void UpdatePreviewFromUrl(string? url, bool isBanner)
    {
        ImageCacheService.LoadBitmapAsync(url, bmp =>
        {
            if (isBanner)
            {
                var img = this.FindControl<Image>("BannerPreviewImage");
                if (img != null)
                {
                    img.Source = bmp;
                    img.IsVisible = bmp != null;
                }
            }
            else
            {
                var img = this.FindControl<Image>("AvatarPreviewImage");
                if (img != null)
                {
                    img.Source = bmp;
                    img.IsVisible = bmp != null;
                }
            }
        }, decodeWidth: isBanner ? 1280 : 256);
    }

    #endregion

    #region Preview click-targets

    private void WirePreviewClickTarget(string targetName, string overlayName, bool isBanner)
    {
        var target = this.FindControl<Control>(targetName);
        var overlay = this.FindControl<Border>(overlayName);
        if (target == null) return;

        target.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(target).Properties.IsLeftButtonPressed)
                _ = PickAndUploadAsync(isBanner);
        };

        if (overlay != null)
        {
            target.PointerEntered += (_, _) => overlay.IsVisible = true;
            target.PointerExited += (_, _) => overlay.IsVisible = false;
        }
    }

    #endregion

    #region Pick & Upload

    private async Task PickAndUploadAsync(bool isBanner)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var serverUrl = isBanner
            ? _bannerBlossomServerTextBox?.Text?.Trim()
            : _profileBlossomServerTextBox?.Text?.Trim();

        if (string.IsNullOrWhiteSpace(serverUrl) || !Uri.TryCreate(serverUrl, UriKind.Absolute, out _))
        {
            SetStatus(isBanner, "Please enter a valid Blossom server URL.", isError: true);
            return;
        }

        // Step 1 — file picker
        byte[] fileBytes;
        string contentType;
        string fileName;
        try
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = isBanner ? "Select Banner Image" : "Select Profile Image",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Images")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif" },
                        MimeTypes = new[] { "image/png", "image/jpeg", "image/webp", "image/gif" }
                    }
                }
            });

            if (files.Count == 0) return; // user cancelled — no error

            var file = files[0];
            await using var stream = await file.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);

            fileBytes = ms.ToArray();
            fileName = file.Name;
            var ext = Path.GetExtension(file.Name).ToLowerInvariant();
            contentType = ext switch
            {
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "File pick failed");
            SetStatus(isBanner, $"Could not read file: {ex.Message}", isError: true);
            return;
        }

        // Step 2 — upload
        SetUploadInProgress(isBanner, true);
        SetStatus(isBanner, $"Uploading {fileName} to {serverUrl}…", isError: false);

        try
        {
            var nostrKeyHex = await GetNostrPrivateKeyHexAsync();
            if (nostrKeyHex == null)
            {
                SetStatus(isBanner, "No wallet selected or unable to access wallet keys.", isError: true);
                return;
            }

            var result = await _blossomService.UploadAsync(serverUrl, fileBytes, contentType, nostrKeyHex);

            if (result.IsFailure)
            {
                SetStatus(isBanner, $"Upload failed: {result.Error}", isError: true);
                return;
            }

            // Set URL on ViewModel — the reactive subscription will update the preview
            if (DataContext is CreateProjectViewModel vm)
            {
                if (isBanner)
                    vm.BannerUrl = result.Value;
                else
                    vm.ProfileUrl = result.Value;
            }

            SetStatus(isBanner, $"Uploaded {fileName} — click again to replace.", isError: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PickAndUploadAsync failed");
            SetStatus(isBanner, $"Upload error: {ex.Message}", isError: true);
        }
        finally
        {
            SetUploadInProgress(isBanner, false);
        }
    }

    private void SetStatus(bool isBanner, string message, bool isError)
    {
        var statusText = isBanner ? _bannerStatusText : _profileStatusText;
        if (statusText == null) return;
        statusText.Text = message;
        statusText.IsVisible = !string.IsNullOrEmpty(message);
        if (Application.Current?.Resources.TryGetResource(
            isError ? "ErrorFieldText" : "TextMuted",
            Avalonia.Styling.ThemeVariant.Default,
            out var brush) == true && brush is Avalonia.Media.IBrush b)
        {
            statusText.Foreground = b;
        }
    }

    private void SetUploadInProgress(bool isBanner, bool inProgress)
    {
        var uploadBtn = isBanner ? _bannerUploadBtn : _profileUploadBtn;
        if (uploadBtn != null) uploadBtn.IsEnabled = !inProgress;

        var iconName = isBanner ? "BannerUploadIcon" : "ProfileUploadIcon";
        var spinnerName = isBanner ? "BannerUploadSpinner" : "ProfileUploadSpinner";

        var icon = this.FindControl<Optris.Icons.Avalonia.Icon>(iconName);
        var spinner = this.FindControl<Optris.Icons.Avalonia.Icon>(spinnerName);

        if (icon != null) icon.IsVisible = !inProgress;
        if (spinner != null) spinner.IsVisible = inProgress;
    }

    #endregion

    /// <summary>
    /// Gets the Nostr private key (hex) from the currently selected wallet for BUD-02 auth.
    /// Uses the wallet-level storage key (no project/founder key required).
    /// </summary>
    private async Task<string?> GetNostrPrivateKeyHexAsync()
    {
        try
        {
            var walletContext = App.Services.GetRequiredService<IWalletContext>();
            var selectedWallet = walletContext.SelectedWallet;
            if (selectedWallet == null)
            {
                _logger.LogWarning(
                    "No wallet selected for Blossom auth, using an ephemeral key for this upload only");
                return BlossomAuthKeyHelper.CreateEphemeralPrivateKeyHex();
            }

            var seedwordsProvider = App.Services.GetRequiredService<ISeedwordsProvider>();
            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(selectedWallet.Id.Value);
            if (sensitiveDataResult.IsFailure)
            {
                _logger.LogWarning("Failed to get wallet sensitive data: {Error}", sensitiveDataResult.Error);
                return null;
            }

            var (words, passphrase) = sensitiveDataResult.Value;
            var walletWords = new WalletWords
            {
                Words = words,
                Passphrase = passphrase.HasValue ? passphrase.Value : null
            };

            var derivation = App.Services.GetRequiredService<IDerivationOperations>();
            var key = derivation.DeriveNostrStorageKey(walletWords);
            return Convert.ToHexString(key.ToBytes()).ToLowerInvariant();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to derive Nostr key for Blossom auth");
            return null;
        }
    }

    /// <summary>
    /// Reset image previews to default state (called by parent on wizard reset).
    /// </summary>
    public void ResetVisualState()
    {
        if (_bannerStatusText != null) _bannerStatusText.IsVisible = false;
        if (_profileStatusText != null) _profileStatusText.IsVisible = false;

        var bannerImg = this.FindControl<Image>("BannerPreviewImage");
        if (bannerImg != null) { bannerImg.Source = null; bannerImg.IsVisible = false; }

        var avatarImg = this.FindControl<Image>("AvatarPreviewImage");
        if (avatarImg != null) { avatarImg.Source = null; avatarImg.IsVisible = false; }
    }
}

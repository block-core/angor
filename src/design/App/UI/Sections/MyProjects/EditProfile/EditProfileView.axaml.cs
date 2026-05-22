using System;
using System.IO;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Sdk.Common;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using App.UI.Shared;
using App.UI.Shell;
using App.UI.Shared.Services;
using Blockcore.NBitcoin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReactiveUI;

namespace App.UI.Sections.MyProjects.EditProfile;

/// <summary>
/// Code-behind for the Edit Project Profile view.
/// Handles tab switching, button wiring, and navigation.
/// </summary>
public partial class EditProfileView : UserControl
{
    private EditProfileViewModel? Vm => DataContext as EditProfileViewModel;
    private readonly ILogger<EditProfileView> _logger;
    private readonly BlossomUploadService _blossomService;

    private IDisposable? _layoutSubscription;
    private IDisposable? _tabSubscription;
    private EditProfileViewModel? _subscribedVm;

    // Blossom upload state
    private byte[]? _picFileBytes;
    private string _picContentType = "image/jpeg";
    private byte[]? _bannerFileBytes;
    private string _bannerContentType = "image/jpeg";

    // Cached blossom upload controls
    private TextBox? _picBlossomServerBox;
    private TextBlock? _picFileNameText;
    private TextBlock? _picStatusText;
    private Button? _picUploadBtn;
    private TextBox? _bannerBlossomServerBox;
    private TextBlock? _bannerFileNameText;
    private TextBlock? _bannerStatusText;
    private Button? _bannerUploadBtn;

    // Tab content panels
    private StackPanel? _profileTabContent;
    private StackPanel? _projectTabContent;
    private StackPanel? _faqTabContent;
    private StackPanel? _membersTabContent;
    private StackPanel? _mediaTabContent;
    private StackPanel? _relaysTabContent;

    // Responsive controls
    private DockPanel? _editNavBar;
    private StackPanel? _editContentStack;
    private Panel? _editNavSpacer;
    private StackPanel? _mobileStickyEditHeader;
    private Border? _editTabsBorder;
    private Grid? _profileMainGrid;
    private Border? _profilePictureCard;
    private Border? _basicInfoCard;
    private Grid? _basicInfoGrid;
    private StackPanel? _usernameField;
    private StackPanel? _displayNameField;
    private Grid? _additionalInfoGrid;
    private StackPanel? _nip05Field;
    private StackPanel? _lightningField;
    private StackPanel? _websiteField;
    private Grid? _memberInputGrid;
    private Grid? _mediaInputGrid;
    private Grid? _relayInputGrid;
    private Button? _addMemberButton;
    private ComboBox? _mediaTypeCombo;
    private Button? _addMediaButton;
    private Button? _addRelayButton;

    // Tab buttons
    private Button? _tabProfile;
    private Button? _tabProject;
    private Button? _tabFaq;
    private Button? _tabMembers;
    private Button? _tabMedia;
    private Button? _tabRelays;
    private Button? _mobileTabProfile;
    private Button? _mobileTabProject;
    private Button? _mobileTabFaq;
    private Button? _mobileTabMembers;
    private Button? _mobileTabMedia;
    private Button? _mobileTabRelays;

    public EditProfileView()
    {
        InitializeComponent();
        _logger = App.Services.GetRequiredService<ILoggerFactory>().CreateLogger<EditProfileView>();
        _blossomService = App.Services.GetRequiredService<BlossomUploadService>();

        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
            Classes.Add("Mobile");

        WireControls();

        DataContextChanged += OnDataContextChanged;

        _layoutSubscription = LayoutModeService.Instance
            .WhenAnyValue(x => x.IsCompact)
            .Subscribe(ApplyResponsiveLayout);
    }

    private void WireControls()
    {
        _editNavBar = this.FindControl<DockPanel>("EditNavBar");
        _editContentStack = this.FindControl<StackPanel>("EditContentStack");
        _editNavSpacer = this.FindControl<Panel>("EditNavSpacer");
        _mobileStickyEditHeader = this.FindControl<StackPanel>("MobileStickyEditHeader");
        _editTabsBorder = this.FindControl<Border>("EditTabsBorder");
        _profileMainGrid = this.FindControl<Grid>("ProfileMainGrid");
        _profilePictureCard = this.FindControl<Border>("ProfilePictureCard");
        _basicInfoCard = this.FindControl<Border>("BasicInfoCard");
        _basicInfoGrid = this.FindControl<Grid>("BasicInfoGrid");
        _usernameField = this.FindControl<StackPanel>("UsernameField");
        _displayNameField = this.FindControl<StackPanel>("DisplayNameField");
        _additionalInfoGrid = this.FindControl<Grid>("AdditionalInfoGrid");
        _nip05Field = this.FindControl<StackPanel>("Nip05Field");
        _lightningField = this.FindControl<StackPanel>("LightningField");
        _websiteField = this.FindControl<StackPanel>("WebsiteField");
        _memberInputGrid = this.FindControl<Grid>("MemberInputGrid");
        _mediaInputGrid = this.FindControl<Grid>("MediaInputGrid");
        _relayInputGrid = this.FindControl<Grid>("RelayInputGrid");

        // Cache tab content panels
        _profileTabContent = this.FindControl<StackPanel>("ProfileTabContent");
        _projectTabContent = this.FindControl<StackPanel>("ProjectTabContent");
        _faqTabContent = this.FindControl<StackPanel>("FaqTabContent");
        _membersTabContent = this.FindControl<StackPanel>("MembersTabContent");
        _mediaTabContent = this.FindControl<StackPanel>("MediaTabContent");
        _relaysTabContent = this.FindControl<StackPanel>("RelaysTabContent");

        // Cache tab buttons
        _tabProfile = this.FindControl<Button>("TabProfile");
        _tabProject = this.FindControl<Button>("TabProject");
        _tabFaq = this.FindControl<Button>("TabFaq");
        _tabMembers = this.FindControl<Button>("TabMembers");
        _tabMedia = this.FindControl<Button>("TabMedia");
        _tabRelays = this.FindControl<Button>("TabRelays");
        _mobileTabProfile = this.FindControl<Button>("MobileTabProfile");
        _mobileTabProject = this.FindControl<Button>("MobileTabProject");
        _mobileTabFaq = this.FindControl<Button>("MobileTabFaq");
        _mobileTabMembers = this.FindControl<Button>("MobileTabMembers");
        _mobileTabMedia = this.FindControl<Button>("MobileTabMedia");
        _mobileTabRelays = this.FindControl<Button>("MobileTabRelays");

        // Wire tab button clicks
        WireTabButton(_tabProfile, "profile");
        WireTabButton(_tabProject, "project");
        WireTabButton(_tabFaq, "faq");
        WireTabButton(_tabMembers, "members");
        WireTabButton(_tabMedia, "media");
        WireTabButton(_tabRelays, "relays");
        WireTabButton(_mobileTabProfile, "profile");
        WireTabButton(_mobileTabProject, "project");
        WireTabButton(_mobileTabFaq, "faq");
        WireTabButton(_mobileTabMembers, "members");
        WireTabButton(_mobileTabMedia, "media");
        WireTabButton(_mobileTabRelays, "relays");

        // Wire action buttons
        var saveBtn = this.FindControl<Button>("SaveButton");
        if (saveBtn != null) saveBtn.Click += OnSaveClick;

        var mobileSaveBtn = this.FindControl<Button>("MobileSaveButton");
        if (mobileSaveBtn != null) mobileSaveBtn.Click += OnSaveClick;

        var addFaqBtn = this.FindControl<Button>("AddFaqButton");
        if (addFaqBtn != null) addFaqBtn.Click += (_, _) => Vm?.AddFaqItem();

        _addMemberButton = this.FindControl<Button>("AddMemberButton");
        if (_addMemberButton != null) _addMemberButton.Click += (_, _) => Vm?.AddMember();

        _mediaTypeCombo = this.FindControl<ComboBox>("MediaTypeCombo");
        _addMediaButton = this.FindControl<Button>("AddMediaButton");
        if (_addMediaButton != null) _addMediaButton.Click += (_, _) => Vm?.AddMedia();

        _addRelayButton = this.FindControl<Button>("AddRelayButton");
        if (_addRelayButton != null) _addRelayButton.Click += (_, _) => Vm?.AddRelay();

        // Wire Blossom upload controls for profile picture
        _picBlossomServerBox = this.FindControl<TextBox>("PicBlossomServerBox");
        _picFileNameText = this.FindControl<TextBlock>("PicFileNameText");
        _picStatusText = this.FindControl<TextBlock>("PicStatusText");
        _picUploadBtn = this.FindControl<Button>("PicUploadBtn");

        var picBrowseBtn = this.FindControl<Button>("PicBrowseBtn");
        if (picBrowseBtn != null)
            picBrowseBtn.Click += (_, _) => _ = BrowseFileAsync(false);
        if (_picUploadBtn != null)
            _picUploadBtn.Click += (_, _) => _ = UploadToBlossomAsync(false);

        // Wire Blossom upload controls for banner
        _bannerBlossomServerBox = this.FindControl<TextBox>("BannerBlossomServerBox");
        _bannerFileNameText = this.FindControl<TextBlock>("BannerFileNameText");
        _bannerStatusText = this.FindControl<TextBlock>("BannerStatusText");
        _bannerUploadBtn = this.FindControl<Button>("BannerUploadBtn");

        var bannerBrowseBtn = this.FindControl<Button>("BannerBrowseBtn");
        if (bannerBrowseBtn != null)
            bannerBrowseBtn.Click += (_, _) => _ = BrowseFileAsync(true);
        if (_bannerUploadBtn != null)
            _bannerUploadBtn.Click += (_, _) => _ = UploadToBlossomAsync(true);

        // Wire remove buttons in item templates via bubbling
        AddHandler(Button.ClickEvent, OnItemButtonClick, RoutingStrategies.Bubble);
    }

    private void ApplyResponsiveLayout(bool isCompact)
    {
        if (_editNavBar != null)
            _editNavBar.IsVisible = !isCompact;
        if (_mobileStickyEditHeader != null)
            _mobileStickyEditHeader.IsVisible = isCompact;
        if (_editTabsBorder != null)
            _editTabsBorder.IsVisible = !isCompact;
        if (_editNavSpacer != null)
            _editNavSpacer.Height = isCompact ? 156 : 92;

        if (_editContentStack != null)
        {
            _editContentStack.Spacing = isCompact ? 16 : 24;
            _editContentStack.Margin = isCompact
                ? new Thickness(16, 0, 16, 96)
                : new Thickness(24, 0, 24, 24);
        }

        ApplyProfileLayout(isCompact);
        ApplyInputGridLayout(_memberInputGrid, _addMemberButton, isCompact, 2);
        ApplyMediaInputLayout(isCompact);
        ApplyInputGridLayout(_relayInputGrid, _addRelayButton, isCompact, 2);
    }

    private void ApplyProfileLayout(bool isCompact)
    {
        if (_profileMainGrid?.ColumnDefinitions.Count >= 2 && _profileMainGrid.RowDefinitions.Count >= 2)
        {
            _profileMainGrid.ColumnDefinitions[0].Width = GridLength.Star;
            _profileMainGrid.ColumnDefinitions[1].Width = isCompact ? new GridLength(0) : GridLength.Star;
            _profileMainGrid.ColumnSpacing = isCompact ? 0 : 20;

            if (_profilePictureCard != null)
            {
                Grid.SetColumn(_profilePictureCard, 0);
                Grid.SetRow(_profilePictureCard, 0);
            }

            if (_basicInfoCard != null)
            {
                Grid.SetColumn(_basicInfoCard, isCompact ? 0 : 1);
                Grid.SetRow(_basicInfoCard, isCompact ? 1 : 0);
            }
        }

        if (_basicInfoGrid?.ColumnDefinitions.Count >= 2 && _basicInfoGrid.RowDefinitions.Count >= 2)
        {
            _basicInfoGrid.ColumnDefinitions[0].Width = GridLength.Star;
            _basicInfoGrid.ColumnDefinitions[1].Width = isCompact ? new GridLength(0) : GridLength.Star;
            _basicInfoGrid.ColumnSpacing = isCompact ? 0 : 16;

            if (_usernameField != null)
            {
                Grid.SetColumn(_usernameField, 0);
                Grid.SetRow(_usernameField, 0);
            }

            if (_displayNameField != null)
            {
                Grid.SetColumn(_displayNameField, isCompact ? 0 : 1);
                Grid.SetRow(_displayNameField, isCompact ? 1 : 0);
            }
        }

        if (_additionalInfoGrid?.ColumnDefinitions.Count >= 3 && _additionalInfoGrid.RowDefinitions.Count >= 3)
        {
            _additionalInfoGrid.ColumnDefinitions[0].Width = GridLength.Star;
            _additionalInfoGrid.ColumnDefinitions[1].Width = isCompact ? new GridLength(0) : GridLength.Star;
            _additionalInfoGrid.ColumnDefinitions[2].Width = isCompact ? new GridLength(0) : GridLength.Star;
            _additionalInfoGrid.ColumnSpacing = isCompact ? 0 : 16;

            if (_nip05Field != null)
            {
                Grid.SetColumn(_nip05Field, 0);
                Grid.SetRow(_nip05Field, 0);
            }

            if (_lightningField != null)
            {
                Grid.SetColumn(_lightningField, isCompact ? 0 : 1);
                Grid.SetRow(_lightningField, isCompact ? 1 : 0);
            }

            if (_websiteField != null)
            {
                Grid.SetColumn(_websiteField, isCompact ? 0 : 2);
                Grid.SetRow(_websiteField, isCompact ? 2 : 0);
            }
        }
    }

    private static void ApplyInputGridLayout(Grid? grid, Control? actionButton, bool isCompact, int columnCount)
    {
        if (grid == null || actionButton == null) return;

        if (grid.ColumnDefinitions.Count >= columnCount)
        {
            grid.ColumnDefinitions[0].Width = GridLength.Star;
            for (int i = 1; i < columnCount; i++)
                grid.ColumnDefinitions[i].Width = isCompact ? new GridLength(0) : GridLength.Auto;
        }

        Grid.SetColumn(actionButton, isCompact ? 0 : columnCount - 1);
        Grid.SetRow(actionButton, isCompact ? 1 : 0);
        actionButton.HorizontalAlignment = isCompact
            ? Avalonia.Layout.HorizontalAlignment.Stretch
            : Avalonia.Layout.HorizontalAlignment.Left;
    }

    private void ApplyMediaInputLayout(bool isCompact)
    {
        if (_mediaInputGrid == null) return;

        if (_mediaInputGrid.ColumnDefinitions.Count >= 3)
        {
            _mediaInputGrid.ColumnDefinitions[0].Width = GridLength.Star;
            _mediaInputGrid.ColumnDefinitions[1].Width = isCompact ? new GridLength(0) : new GridLength(140);
            _mediaInputGrid.ColumnDefinitions[2].Width = isCompact ? new GridLength(0) : GridLength.Auto;
        }

        if (_mediaTypeCombo != null)
        {
            Grid.SetColumn(_mediaTypeCombo, isCompact ? 0 : 1);
            Grid.SetRow(_mediaTypeCombo, isCompact ? 1 : 0);
            _mediaTypeCombo.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        }

        if (_addMediaButton != null)
        {
            Grid.SetColumn(_addMediaButton, isCompact ? 0 : 2);
            Grid.SetRow(_addMediaButton, isCompact ? 2 : 0);
            _addMediaButton.HorizontalAlignment = isCompact
                ? Avalonia.Layout.HorizontalAlignment.Stretch
                : Avalonia.Layout.HorizontalAlignment.Left;
        }
    }

    private void WireTabButton(Button? btn, string tabId)
    {
        if (btn == null) return;
        btn.Click += (_, _) =>
        {
            Vm?.SetActiveTab(tabId);
            UpdateTabVisuals(tabId);
        };
    }

    private void UpdateTabVisuals(string activeTab)
    {
        // Show/hide tab content
        if (_profileTabContent != null) _profileTabContent.IsVisible = activeTab == "profile";
        if (_projectTabContent != null) _projectTabContent.IsVisible = activeTab == "project";
        if (_faqTabContent != null) _faqTabContent.IsVisible = activeTab == "faq";
        if (_membersTabContent != null) _membersTabContent.IsVisible = activeTab == "members";
        if (_mediaTabContent != null) _mediaTabContent.IsVisible = activeTab == "media";
        if (_relaysTabContent != null) _relaysTabContent.IsVisible = activeTab == "relays";

        // Update tab button active state
        SetTabActive(_tabProfile, activeTab == "profile");
        SetTabActive(_tabProject, activeTab == "project");
        SetTabActive(_tabFaq, activeTab == "faq");
        SetTabActive(_tabMembers, activeTab == "members");
        SetTabActive(_tabMedia, activeTab == "media");
        SetTabActive(_tabRelays, activeTab == "relays");
        SetTabActive(_mobileTabProfile, activeTab == "profile");
        SetTabActive(_mobileTabProject, activeTab == "project");
        SetTabActive(_mobileTabFaq, activeTab == "faq");
        SetTabActive(_mobileTabMembers, activeTab == "members");
        SetTabActive(_mobileTabMedia, activeTab == "media");
        SetTabActive(_mobileTabRelays, activeTab == "relays");
    }

    private static void SetTabActive(Button? btn, bool active)
    {
        if (btn == null) return;
        btn.Classes.Set("TabActive", active);
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        SubscribeToViewModel();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        SubscribeToViewModel();
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;
        DataContextChanged -= OnDataContextChanged;
        UnsubscribeFromViewModel();

        base.OnDetachedFromLogicalTree(e);
    }

    private void SubscribeToViewModel()
    {
        if (Vm == _subscribedVm)
            return;

        UnsubscribeFromViewModel();

        if (Vm == null)
            return;

        _subscribedVm = Vm;
        _subscribedVm.ToastRequested += OnToastRequested;

        _tabSubscription = _subscribedVm.WhenAnyValue(x => x.ActiveTab)
            .Subscribe(tab => UpdateTabVisuals(tab ?? "profile"));

        UpdateTabVisuals(_subscribedVm.ActiveTab ?? "profile");
    }

    private void UnsubscribeFromViewModel()
    {
        _tabSubscription?.Dispose();
        _tabSubscription = null;

        if (_subscribedVm != null)
            _subscribedVm.ToastRequested -= OnToastRequested;

        _subscribedVm = null;
    }

    private void OnToastRequested(string message)
    {
        var shellVm = this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;
        shellVm?.ShowToast(message);
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (Vm != null)
                await Vm.SaveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OnSaveClick failed");
        }
    }

    private void OnItemButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        switch (btn.Name)
        {
            case "RemoveFaqButton":
                if (btn.DataContext is FaqItemViewModel faqItem)
                {
                    Vm?.RemoveFaqItem(faqItem);
                    e.Handled = true;
                }
                break;

            case "RemoveMemberButton":
                if (btn.DataContext is string memberPubKey)
                {
                    Vm?.RemoveMember(memberPubKey);
                    e.Handled = true;
                }
                break;

            case "RemoveMediaButton":
                if (btn.DataContext is MediaItemViewModel mediaItem)
                {
                    Vm?.RemoveMedia(mediaItem);
                    e.Handled = true;
                }
                break;

            case "RemoveRelayButton":
                if (btn.DataContext is string relayUrl)
                {
                    Vm?.RemoveRelay(relayUrl);
                    e.Handled = true;
                }
                break;
        }
    }

    // Track the back button handler to prevent accumulation
    private Button? _backBtn;
    private EventHandler<RoutedEventArgs>? _backClickHandler;

    #region Blossom Upload

    private async Task BrowseFileAsync(bool isBanner)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        try
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = isBanner ? "Select Banner Image" : "Select Profile Picture",
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

            if (files.Count == 0) return;

            var file = files[0];
            await using var stream = await file.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);

            var bytes = ms.ToArray();
            var ext = Path.GetExtension(file.Name).ToLowerInvariant();
            var contentType = ext switch
            {
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };

            if (isBanner)
            {
                _bannerFileBytes = bytes;
                _bannerContentType = contentType;
                if (_bannerFileNameText != null)
                    _bannerFileNameText.Text = $"{file.Name} ({bytes.Length / 1024} KB)";
                SetBlossomStatus(true, $"Ready to upload: {file.Name}", isError: false);
            }
            else
            {
                _picFileBytes = bytes;
                _picContentType = contentType;
                if (_picFileNameText != null)
                    _picFileNameText.Text = $"{file.Name} ({bytes.Length / 1024} KB)";
                SetBlossomStatus(false, $"Ready to upload: {file.Name}", isError: false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Browse file failed");
        }
    }

    private async Task UploadToBlossomAsync(bool isBanner)
    {
        var fileBytes = isBanner ? _bannerFileBytes : _picFileBytes;
        var contentType = isBanner ? _bannerContentType : _picContentType;
        var serverUrl = isBanner
            ? _bannerBlossomServerBox?.Text?.Trim()
            : _picBlossomServerBox?.Text?.Trim();

        if (fileBytes == null || fileBytes.Length == 0)
        {
            SetBlossomStatus(isBanner, "Please browse and select a file first.", isError: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(serverUrl) || !Uri.TryCreate(serverUrl, UriKind.Absolute, out _))
        {
            SetBlossomStatus(isBanner, "Please enter a valid Blossom server URL.", isError: true);
            return;
        }

        SetBlossomUploadInProgress(isBanner, true);
        SetBlossomStatus(isBanner, $"Uploading to {serverUrl}…", isError: false);

        try
        {
            var nostrKeyHex = await GetNostrPrivateKeyHexAsync();
            if (nostrKeyHex == null)
            {
                SetBlossomStatus(isBanner, "No wallet selected or unable to access wallet keys.", isError: true);
                SetBlossomUploadInProgress(isBanner, false);
                return;
            }

            var result = await _blossomService.UploadAsync(serverUrl, fileBytes, contentType, nostrKeyHex);

            if (result.IsFailure)
            {
                SetBlossomStatus(isBanner, $"Upload failed: {result.Error}", isError: true);
                return;
            }

            // Update ViewModel URL — the reactive subscription will reload the preview
            if (Vm != null)
            {
                if (isBanner)
                    Vm.ProfileBanner = result.Value;
                else
                    Vm.ProfilePicture = result.Value;
            }

            SetBlossomStatus(isBanner, "Upload successful!", isError: false);

            if (isBanner)
            {
                _bannerFileBytes = null;
                if (_bannerFileNameText != null) _bannerFileNameText.Text = "No file selected";
            }
            else
            {
                _picFileBytes = null;
                if (_picFileNameText != null) _picFileNameText.Text = "No file selected";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UploadToBlossomAsync failed");
            SetBlossomStatus(isBanner, $"Upload error: {ex.Message}", isError: true);
        }
        finally
        {
            SetBlossomUploadInProgress(isBanner, false);
        }
    }

    private void SetBlossomStatus(bool isBanner, string message, bool isError)
    {
        var statusText = isBanner ? _bannerStatusText : _picStatusText;
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

    private void SetBlossomUploadInProgress(bool isBanner, bool inProgress)
    {
        var btn = isBanner ? _bannerUploadBtn : _picUploadBtn;
        if (btn != null) btn.IsEnabled = !inProgress;

        var iconName = isBanner ? "BannerUploadIcon" : "PicUploadIcon";
        var spinnerName = isBanner ? "BannerUploadSpinner" : "PicUploadSpinner";

        var icon = this.FindControl<Optris.Icons.Avalonia.Icon>(iconName);
        var spinner = this.FindControl<Optris.Icons.Avalonia.Icon>(spinnerName);

        if (icon != null) icon.IsVisible = !inProgress;
        if (spinner != null) spinner.IsVisible = inProgress;
    }

    #endregion

    /// <summary>
    /// Gets the Nostr private key (hex) from the currently selected wallet for BUD-02 auth.
    /// </summary>
    private async Task<string?> GetNostrPrivateKeyHexAsync()
    {
        try
        {
            var walletContext = App.Services.GetRequiredService<IWalletContext>();
            var selectedWallet = walletContext.SelectedWallet;
            if (selectedWallet == null)
            {
                _logger.LogWarning("No wallet selected for Blossom auth");
                return null;
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

    /// <summary>Wire the Back button to navigate back to the project list.</summary>
    public void SetBackAction(Action backAction)
    {
        _backBtn ??= this.FindControl<Button>("EditBackButton");
        if (_backBtn == null) return;

        if (_backClickHandler != null)
            _backBtn.Click -= _backClickHandler;

        _backClickHandler = (_, _) => backAction();
        _backBtn.Click += _backClickHandler;
    }
}

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using App.UI.Shared;
using App.UI.Shell;
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

    private IDisposable? _layoutSubscription;
    private IDisposable? _tabSubscription;

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
    private StackPanel? _mobileEditActions;
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

    public EditProfileView()
    {
        InitializeComponent();
        _logger = App.Services.GetRequiredService<ILoggerFactory>().CreateLogger<EditProfileView>();

        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
            Classes.Add("Mobile");

        WireControls();

        _layoutSubscription = LayoutModeService.Instance
            .WhenAnyValue(x => x.IsCompact)
            .Subscribe(ApplyResponsiveLayout);
    }

    private void WireControls()
    {
        _editNavBar = this.FindControl<DockPanel>("EditNavBar");
        _editContentStack = this.FindControl<StackPanel>("EditContentStack");
        _editNavSpacer = this.FindControl<Panel>("EditNavSpacer");
        _mobileEditActions = this.FindControl<StackPanel>("MobileEditActions");
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

        // Wire tab button clicks
        WireTabButton(_tabProfile, "profile");
        WireTabButton(_tabProject, "project");
        WireTabButton(_tabFaq, "faq");
        WireTabButton(_tabMembers, "members");
        WireTabButton(_tabMedia, "media");
        WireTabButton(_tabRelays, "relays");

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

        // Wire remove buttons in item templates via bubbling
        AddHandler(Button.ClickEvent, OnItemButtonClick, RoutingStrategies.Bubble);
    }

    private void ApplyResponsiveLayout(bool isCompact)
    {
        if (_editNavBar != null)
            _editNavBar.IsVisible = !isCompact;
        if (_mobileEditActions != null)
            _mobileEditActions.IsVisible = isCompact;
        if (_editNavSpacer != null)
            _editNavSpacer.Height = isCompact ? 16 : 92;

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
    }

    private static void SetTabActive(Button? btn, bool active)
    {
        if (btn == null) return;
        btn.Classes.Set("TabActive", active);
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);

        if (DataContext is EditProfileViewModel vm)
        {
            // Subscribe to toast notifications
            vm.ToastRequested += OnToastRequested;

            // Subscribe to active tab changes to keep visuals in sync
            _tabSubscription = vm.WhenAnyValue(x => x.ActiveTab)
                .Subscribe(tab => UpdateTabVisuals(tab ?? "profile"));

            // Show initial tab
            UpdateTabVisuals(vm.ActiveTab ?? "profile");
        }
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _tabSubscription?.Dispose();
        _tabSubscription = null;
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;

        if (DataContext is EditProfileViewModel vm)
            vm.ToastRequested -= OnToastRequested;

        base.OnDetachedFromLogicalTree(e);
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

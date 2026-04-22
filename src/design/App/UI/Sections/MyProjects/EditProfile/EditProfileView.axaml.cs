using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
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
        WireControls();
    }

    private void WireControls()
    {
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

        var addFaqBtn = this.FindControl<Button>("AddFaqButton");
        if (addFaqBtn != null) addFaqBtn.Click += (_, _) => Vm?.AddFaqItem();

        var addMemberBtn = this.FindControl<Button>("AddMemberButton");
        if (addMemberBtn != null) addMemberBtn.Click += (_, _) => Vm?.AddMember();

        var addMediaBtn = this.FindControl<Button>("AddMediaButton");
        if (addMediaBtn != null) addMediaBtn.Click += (_, _) => Vm?.AddMedia();

        var addRelayBtn = this.FindControl<Button>("AddRelayButton");
        if (addRelayBtn != null) addRelayBtn.Click += (_, _) => Vm?.AddRelay();

        // Wire remove buttons in item templates via bubbling
        AddHandler(Button.ClickEvent, OnItemButtonClick, RoutingStrategies.Bubble);
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

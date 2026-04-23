using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using App.UI.Sections.MyProjects.EditProfile;
using App.UI.Shared;
using App.UI.Shared.Controls;
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace App.UI.Sections.MyProjects;

public partial class MyProjectsView : UserControl
{
    private CompositeDisposable? _subscriptions;
    private IDisposable? _layoutSubscription;

    // Cached controls for responsive layout
    private Grid? _projectListGrid;
    private Border? _myProjectsSidebar;
    private ScrollableView? _myProjectsContent;
    // Sidebar hero elements — hidden on compact (Vue mobile shows only action buttons)
    private Panel? _sidebarLogo;
    private TextBlock? _sidebarTitle;
    private TextBlock? _sidebarSubtitle;
    private Grid? _sidebarStats;
    private Button? _howFundingWorksBtn;
    private StackPanel? _mobileActionPanel;
    private StackPanel? _sidebarCTAButtons;

    /// <summary>Design-time only.</summary>
    public MyProjectsView() => InitializeComponent();

    public MyProjectsView(MyProjectsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // Set the create wizard's DataContext from the parent VM
        // (CreateProjectView is XAML-embedded, so it can't use constructor injection)
        if (CreateWizardView != null)
            CreateWizardView.DataContext = vm.CreateProjectVm;

        _subscriptions = new CompositeDisposable();

        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);

        // Forward toast notifications from VM to ShellViewModel
        vm.ToastRequested += OnToastRequested;
        Disposable.Create(() => vm.ToastRequested -= OnToastRequested)
            .DisposeWith(_subscriptions);

        // Manage panel visibility based on ViewModel state
        SubscribeToVisibility(vm);

        // Check if we should auto-open the wizard (from Home "Launch a Project" button)
        AttachedToVisualTree += OnAttachedToVisualTree;

        // ── Cache responsive layout controls ──
        _projectListGrid = this.FindControl<Grid>("ProjectListGrid");
        _myProjectsSidebar = this.FindControl<Border>("MyProjectsSidebar");
        _myProjectsContent = this.FindControl<ScrollableView>("MyProjectsContent");
        _sidebarLogo = this.FindControl<Panel>("SidebarLogo");
        _sidebarTitle = this.FindControl<TextBlock>("SidebarTitle");
        _sidebarSubtitle = this.FindControl<TextBlock>("SidebarSubtitle");
        _sidebarStats = this.FindControl<Grid>("SidebarStats");
        _howFundingWorksBtn = this.FindControl<Button>("HowFundingWorksBtn");
        _mobileActionPanel = this.FindControl<StackPanel>("MobileActionPanel");
        _sidebarCTAButtons = this.FindControl<StackPanel>("SidebarCTAButtons");

        // ── Responsive layout: 380px sidebar + content (desktop) → stacked (compact) ──
        // Observe both CurrentMode and WindowWidth to handle the xl (>=1280px) breakpoint
        // which determines sidebar 256 vs 380 px on non-compact layouts.
        _layoutSubscription = LayoutModeService.Instance
            .WhenAnyValue(x => x.CurrentMode, x => x.WindowWidth, (m, _) => m)
            .DistinctUntilChanged(mode => (mode, LayoutModeService.Instance.WindowWidth >= 1280))
            .Subscribe(mode => ApplyResponsiveLayout(mode));
    }

    private void ApplyResponsiveLayout(LayoutMode mode)
    {
        if (_projectListGrid == null || _myProjectsSidebar == null || _myProjectsContent == null) return;

        bool isCompact = mode != LayoutMode.Desktop;
        // Prototype breakpoints: xl (>=1280) uses w-96 (384px) + gap-8 + p-6,
        // md..xl (>=768 and <1280) uses w-64 (256px) + gap-4 + p-4.
        // Our LayoutModeService has Tablet (768-1023) and Desktop (>=1024) — we map both
        // non-compact modes to xl-equivalent since desktop users typically have >=1280.
        // Tablet 1024-1279 gets the narrower w-64 sidebar + tighter spacing.
        bool isXl = mode == LayoutMode.Desktop && LayoutModeService.Instance.WindowWidth >= 1280;

        double sidebarWidth = isXl ? 380 : 256;
        double gridPadding = isXl ? 24 : 16;
        double columnGap = isXl ? 32 : 16;

        // CRITICAL: modify existing column/row widths in-place — never Clear()+Add().
        var cols = _projectListGrid.ColumnDefinitions;
        var rows = _projectListGrid.RowDefinitions;

        // Hide sidebar hero content on compact — Vue mobile shows only action buttons
        if (_sidebarLogo != null) _sidebarLogo.IsVisible = !isCompact;
        if (_sidebarTitle != null) _sidebarTitle.IsVisible = !isCompact;
        if (_sidebarSubtitle != null) _sidebarSubtitle.IsVisible = !isCompact;
        if (_sidebarStats != null) _sidebarStats.IsVisible = !isCompact;
        if (_howFundingWorksBtn != null) _howFundingWorksBtn.IsVisible = !isCompact;
        if (_mobileActionPanel != null) _mobileActionPanel.IsVisible = isCompact;
        if (_sidebarCTAButtons != null) _sidebarCTAButtons.IsVisible = !isCompact;

        if (isCompact)
        {
            // Collapse sidebar column, use rows for stacked layout
            if (cols.Count >= 2) { cols[0].Width = GridLength.Star; cols[1].Width = new GridLength(0); }
            if (rows.Count >= 2) { rows[0].Height = GridLength.Auto; rows[1].Height = GridLength.Star; }

            Grid.SetColumn(_myProjectsSidebar, 0);
            Grid.SetRow(_myProjectsSidebar, 0);
            _myProjectsSidebar.Margin = new Avalonia.Thickness(0, 0, 0, 16);
            _myProjectsSidebar.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            // Strip card styling — just show the button
            _myProjectsSidebar.Background = null;
            _myProjectsSidebar.BorderThickness = new Avalonia.Thickness(0);
            _myProjectsSidebar.BoxShadow = new Avalonia.Media.BoxShadows(default);
            _myProjectsSidebar.Padding = new Avalonia.Thickness(0);

            Grid.SetColumn(_myProjectsContent, 0);
            Grid.SetRow(_myProjectsContent, 1);
            _myProjectsContent.ContentPadding = new Avalonia.Thickness(0, 0, 0, 96);

            // Tighter outer margin on mobile to match Vue mobile px-4 (App.vue line 549: grid gap-4 px-4)
            _projectListGrid.Margin = new Avalonia.Thickness(16, 16, 16, 16);
        }
        else
        {
            // Side by side: sidebar + * content, single row
            if (cols.Count >= 2) { cols[0].Width = new GridLength(sidebarWidth); cols[1].Width = GridLength.Star; }
            if (rows.Count >= 2) { rows[0].Height = GridLength.Star; rows[1].Height = new GridLength(0); }

            Grid.SetColumn(_myProjectsSidebar, 0);
            Grid.SetRow(_myProjectsSidebar, 0);
            _myProjectsSidebar.Margin = new Avalonia.Thickness(0, 0, columnGap, 0);
            _myProjectsSidebar.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            // Restore card styling — clear local overrides so XAML DynamicResource values take effect
            _myProjectsSidebar.ClearValue(Avalonia.Controls.Border.BackgroundProperty);
            _myProjectsSidebar.ClearValue(Avalonia.Controls.Border.BorderThicknessProperty);
            _myProjectsSidebar.ClearValue(Avalonia.Controls.Border.BoxShadowProperty);
            _myProjectsSidebar.ClearValue(Avalonia.Controls.Border.PaddingProperty);

            Grid.SetColumn(_myProjectsContent, 1);
            Grid.SetRow(_myProjectsContent, 0);
            _myProjectsContent.ContentPadding = new Avalonia.Thickness(0);

            // Outer padding matches Vue p-4 (16) at tablet, p-6 (24) at desktop
            _projectListGrid.Margin = new Avalonia.Thickness(gridPadding);
        }
    }

    /// <summary>
    /// Mobile perf: force a measure pass on the hidden ManageProjectView so its
    /// template, resources, and layout are realized before the first drill-down.
    /// This converts a first-drill "cold inflate + measure + arrange" (~300ms on
    /// Android) into a warm DataContext swap (&lt;100ms).
    /// Called by ShellViewModel after tab pre-warm completes.
    /// Idempotent — safe to call multiple times.
    /// </summary>
    public void PreWarmManageView()
    {
        if (!OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS()) return;
        if (ManageProjectViewControl == null || ManageProjectPanel == null) return;

        // The panel stays IsVisible=false, but we can still force a measure pass
        // on the hidden child to realize its template and resolve all DynamicResource
        // lookups ahead of time. Measure with Size.Infinity is cheap (no arrange,
        // no render) but primes the control tree.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            ManageProjectViewControl.Measure(Size.Infinity);
        }
        catch
        {
            // Measure can throw if the control can't resolve resources yet — safe to skip
        }
        sw.Stop();
        global::App.App.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("ShellPerf")
            .LogInformation("[PreWarm] ManageProjectView measureMs={Ms}", sw.ElapsedMilliseconds);
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        var shell = this.FindAncestorOfType<ShellView>();
        if (shell?.DataContext is ShellViewModel shellVm && shellVm.PendingLaunchWizard)
        {
            shellVm.PendingLaunchWizard = false;
            if (DataContext is MyProjectsViewModel vm)
                OpenCreateWizard(vm);
        }
    }

    private void SubscribeToVisibility(MyProjectsViewModel vm)
    {
        // ShowCreateWizard drives the wizard panel
        vm.WhenAnyValue(x => x.ShowCreateWizard)
            .Subscribe(showWizard =>
            {
                if (CreateWizardPanel != null) CreateWizardPanel.IsVisible = showWizard;
                UpdateListVisibility(vm);
                // Update shell title + mobile detail state
                var shell = this.FindAncestorOfType<ShellView>();
                if (shell?.DataContext is ShellViewModel shellVm)
                {
                    shellVm.SectionTitleOverride = showWizard ? "Create New Project" : null;
                    shellVm.IsCreatingProject = showWizard;
                }
            })
            .DisposeWith(_subscriptions!);

        // HasProjects drives empty state vs project list
        vm.WhenAnyValue(x => x.HasProjects)
            .Subscribe(_ => UpdateListVisibility(vm))
            .DisposeWith(_subscriptions!);

        // SelectedManageProject drives the manage project detail panel
        vm.WhenAnyValue(x => x.SelectedManageProject)
            .Subscribe(manageVm =>
            {
                if (ManageProjectPanel != null)
                    ManageProjectPanel.IsVisible = manageVm != null;

                if (ManageProjectViewControl != null && manageVm != null)
                {
                    ManageProjectViewControl.DataContext = manageVm;
                    ManageProjectViewControl.SetBackAction(() => vm.CloseManageProject());
                }

                UpdateListVisibility(vm);

                // Set shell title + mobile detail state for manage funds
                var shell = this.FindAncestorOfType<ShellView>();
                if (shell?.DataContext is ShellViewModel shellVm)
                {
                    shellVm.IsManageFundsOpen = manageVm != null;

                    if (manageVm != null)
                        shellVm.SectionTitleOverride = manageVm.Project.Name;
                    else if (!vm.ShowCreateWizard && vm.SelectedEditProject == null)
                        shellVm.SectionTitleOverride = null;
                }
            })
            .DisposeWith(_subscriptions!);

        // SelectedEditProject drives the edit profile panel
        vm.WhenAnyValue(x => x.SelectedEditProject)
            .Subscribe(editVm =>
            {
                var editProfilePanel = this.FindControl<Panel>("EditProfilePanel");
                if (editProfilePanel != null)
                    editProfilePanel.IsVisible = editVm != null;

                var editProfileViewControl = this.FindControl<EditProfileView>("EditProfileViewControl");
                if (editProfileViewControl != null && editVm != null)
                {
                    editProfileViewControl.DataContext = editVm;
                    editProfileViewControl.SetBackAction(() => vm.CloseEditProfile());
                }

                UpdateListVisibility(vm);

                // Update shell title
                var shell = this.FindAncestorOfType<ShellView>();
                if (shell?.DataContext is ShellViewModel shellVm)
                {
                    if (editVm != null)
                        shellVm.SectionTitleOverride = $"Edit Profile — {editVm.ProjectName}";
                    else if (!vm.ShowCreateWizard && vm.SelectedManageProject == null)
                        shellVm.SectionTitleOverride = null;
                }
            })
            .DisposeWith(_subscriptions!);
    }

    private void OnToastRequested(string message)
    {
        var shellVm = this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;
        shellVm?.ShowToast(message);
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);

        if (DataContext is not MyProjectsViewModel vm) return;

        // Re-subscribe if subscriptions were disposed (view re-attached from cache)
        if (_subscriptions == null)
        {
            _subscriptions = new CompositeDisposable();
            vm.ToastRequested += OnToastRequested;
            Disposable.Create(() => vm.ToastRequested -= OnToastRequested)
                .DisposeWith(_subscriptions);
            SubscribeToVisibility(vm);
            UpdateListVisibility(vm);
        }

        // Load founder projects each time the view is navigated to
        _ = vm.LoadFounderProjectsAsync();
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _subscriptions?.Dispose();
        _subscriptions = null;
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;
        base.OnDetachedFromLogicalTree(e);
    }

    private void UpdateListVisibility(MyProjectsViewModel vm)
    {
        var showWizard = vm.ShowCreateWizard;
        var showManage = vm.SelectedManageProject != null;
        var showEdit = vm.SelectedEditProject != null;

        if (EmptyStatePanel != null)
            EmptyStatePanel.IsVisible = !showWizard && !showManage && !showEdit && !vm.HasProjects;
        if (ProjectListPanel != null)
            ProjectListPanel.IsVisible = !showWizard && !showManage && !showEdit && vm.HasProjects;
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;
        if (DataContext is not MyProjectsViewModel vm) return;

        switch (btn.Name)
        {
            case "LaunchFromListButton":
            case "MobileLaunchButton":
                OpenCreateWizard(vm);
                return;

            case "ScanProjectsButton":
            case "MobileScanButton":
                _ = vm.ScanForProjectsAsync();
                e.Handled = true;
                return;

            case "PART_ManageButton":
                // Walk up from the button to find the ProjectCard, then get its DataContext
                var element = btn as Control;
                while (element != null && element is not ProjectCard)
                    element = element.Parent as Control;

                if (element is ProjectCard card
                    && card.DataContext is MyProjectItemViewModel project)
                {
                    vm.OpenManageProject(project);
                    e.Handled = true;
                }
                return;

            case "PART_EditButton":
                // Walk up from the button to find the ProjectCard, then get its DataContext
                var editElement = btn as Control;
                while (editElement != null && editElement is not ProjectCard)
                    editElement = editElement.Parent as Control;

                if (editElement is ProjectCard editCard
                    && editCard.DataContext is MyProjectItemViewModel editProject)
                {
                    vm.OpenEditProfile(editProject);
                    e.Handled = true;
                }
                return;

            case "PART_ShareButton":
                // Walk up from the button to find the ProjectCard, then get its DataContext
                var shareElement = btn as Control;
                while (shareElement != null && shareElement is not ProjectCard)
                    shareElement = shareElement.Parent as Control;

                if (shareElement is ProjectCard shareCard
                    && shareCard.DataContext is MyProjectItemViewModel shareProject)
                {
                    var shell = this.FindAncestorOfType<ShellView>();
                    if (shell?.DataContext is ShellViewModel shellVm && !shellVm.IsModalOpen)
                    {
                        var modal = new ShareModal(shareProject.Name, shareProject.Description);
                        shellVm.ShowModal(modal);
                    }
                    e.Handled = true;
                }
                return;
        }

        // EmptyState button doesn't have a Name — check by content
        if (btn.Content is Avalonia.Controls.StackPanel sp)
        {
            foreach (var child in sp.Children)
            {
                if (child is TextBlock tb && tb.Text == "Launch a Project")
                {
                    OpenCreateWizard(vm);
                    return;
                }
            }
        }
        // Also check direct TextBlock content inside button from EmptyState
        if (btn.Content is string s && s == "Launch a Project")
        {
            OpenCreateWizard(vm);
        }
    }

    private void OpenCreateWizard(MyProjectsViewModel vm)
    {
        // Reset wizard VM + visual state so it starts fresh every time
        if (CreateWizardView?.DataContext is CreateProjectViewModel wizardVm)
            wizardVm.ResetWizard();
        if (CreateWizardView is CreateProjectView wizardView)
            wizardView.ResetVisualState();

        vm.LaunchCreateWizard();

        // Wire the wizard's deploy callback
        // Vue ref: goToMyProjects() creates project, adds to list, closes wizard, navigates to my-projects.
        // Both "Go to My Projects" button AND backdrop click on success modal trigger this.
        if (CreateWizardView?.DataContext is CreateProjectViewModel wvm)
        {
            wvm.OnProjectDeployed = () =>
            {
                vm.OnProjectDeployed(wvm);
                vm.CloseCreateWizard(); // Close wizard -> shows my-projects list with new project at top
            };
        }
    }
}

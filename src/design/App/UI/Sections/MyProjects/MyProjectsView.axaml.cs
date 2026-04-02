using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using App.UI.Shared.Controls;
using App.UI.Shell;

namespace App.UI.Sections.MyProjects;

public partial class MyProjectsView : UserControl
{
    private CompositeDisposable? _subscriptions;

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
                // Update shell title
                var shell = this.FindAncestorOfType<ShellView>();
                if (shell?.DataContext is ShellViewModel shellVm)
                    shellVm.SectionTitleOverride = showWizard ? "Create New Project" : null;
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

                // Set shell title to project name when managing
                var shell = this.FindAncestorOfType<ShellView>();
                if (shell?.DataContext is ShellViewModel shellVm)
                {
                    if (manageVm != null)
                        shellVm.SectionTitleOverride = manageVm.Project.Name;
                    else if (!vm.ShowCreateWizard)
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

        // Re-subscribe if subscriptions were disposed (view re-attached from cache)
        if (_subscriptions == null && DataContext is MyProjectsViewModel vm)
        {
            _subscriptions = new CompositeDisposable();
            vm.ToastRequested += OnToastRequested;
            Disposable.Create(() => vm.ToastRequested -= OnToastRequested)
                .DisposeWith(_subscriptions);
            SubscribeToVisibility(vm);
            UpdateListVisibility(vm);
        }
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _subscriptions?.Dispose();
        _subscriptions = null;
        base.OnDetachedFromLogicalTree(e);
    }

    private void UpdateListVisibility(MyProjectsViewModel vm)
    {
        var showWizard = vm.ShowCreateWizard;
        var showManage = vm.SelectedManageProject != null;

        if (EmptyStatePanel != null)
            EmptyStatePanel.IsVisible = !showWizard && !showManage && !vm.HasProjects;
        if (ProjectListPanel != null)
            ProjectListPanel.IsVisible = !showWizard && !showManage && vm.HasProjects;
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;
        if (DataContext is not MyProjectsViewModel vm) return;

        switch (btn.Name)
        {
            case "LaunchFromListButton":
                OpenCreateWizard(vm);
                return;

            case "ScanProjectsButton":
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

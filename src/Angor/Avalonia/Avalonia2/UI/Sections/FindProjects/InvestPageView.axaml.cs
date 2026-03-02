using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia2.UI.Shared.Helpers;
using Avalonia2.UI.Shell;
using ReactiveUI;

namespace Avalonia2.UI.Sections.FindProjects;

public partial class InvestPageView : UserControl
{
    private IDisposable? _screenSubscription;
    private Border? _selectedQuickAmountBorder;
    private Border? _selectedSubPlanBorder;

    public InvestPageView()
    {
        InitializeComponent();

        // Wire up button clicks
        AddHandler(Button.ClickEvent, OnButtonClick);
        // Quick amount + submit + subscription plan border clicks
        AddHandler(Border.PointerPressedEvent, OnBorderPressed, RoutingStrategies.Bubble);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Reset scroll to top when navigating to a new invest page
        var scroller = this.FindControl<ScrollViewer>("ContentScroller");
        scroller?.ScrollToHome();

        _screenSubscription?.Dispose();
        _screenSubscription = null;

        if (DataContext is InvestPageViewModel vm)
        {
            // Watch for screen changes to show/hide shell-level modal
            _screenSubscription = vm.WhenAnyValue(x => x.CurrentScreen)
                .Subscribe(screen =>
                {
                    if (screen != InvestScreen.InvestForm)
                    {
                        ShowShellModal(vm);
                    }
                });

            // If subscription, apply initial plan selection styling after layout.
            // This one-time walk finds the initially-selected plan's border.
            if (vm.IsSubscription)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    // Find the SubPlanBorder whose DataContext has IsSelected == true
                    foreach (var border in this.GetVisualDescendants().OfType<Border>())
                    {
                        if (border.Name == "SubPlanBorder" &&
                            border.DataContext is SubscriptionPlanOption { IsSelected: true })
                        {
                            UpdateSubscriptionPlanSelection(border);
                            break;
                        }
                    }
                }, Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        _screenSubscription?.Dispose();
        _screenSubscription = null;
    }

    private InvestPageViewModel? Vm => DataContext as InvestPageViewModel;

    private ShellViewModel? GetShellVm()
    {
        var shellView = this.FindAncestorOfType<ShellView>();
        return shellView?.DataContext as ShellViewModel;
    }

    /// <summary>
    /// Create InvestModalsView and push it to the shell-level modal overlay.
    /// </summary>
    private void ShowShellModal(InvestPageViewModel vm)
    {
        var shellVm = GetShellVm();
        if (shellVm == null || shellVm.IsModalOpen) return;

        var modalsView = new InvestModalsView
        {
            DataContext = vm,
            OnNavigateBackToList = () =>
            {
                // Add the invested project to the Portfolio section
                AddInvestmentToPortfolio(vm);
                // Navigate to the Funded section to show the new investment
                var shell = GetShellVm();
                shell?.NavigateToFunded();
            }
        };

        shellVm.ShowModal(modalsView);
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        switch (btn.Name)
        {
            // Back button (standardized: Button inside Border)
            case "BackButton":
                NavigateBackToDetail();
                break;
        }
    }

    /// <summary>
    /// Handle clicks on Border elements — quick amounts, submit button, copy project ID,
    /// and subscription plan buttons.
    /// </summary>
    private void OnBorderPressed(object? sender, PointerPressedEventArgs e)
    {
        var source = e.Source as Control;
        Border? found = null;
        string? foundName = null;

        while (source != null)
        {
            if (source is Border b && !string.IsNullOrEmpty(b.Name))
            {
                var name = b.Name;
                if (name == "QuickAmountBorder" || name == "SubmitButton" ||
                    name == "CopyProjectIdButton" || name == "SubPlanBorder")
                {
                    found = b;
                    foundName = name;
                    break;
                }
            }
            source = source.Parent as Control;
        }

        if (found == null || foundName == null) return;

        switch (foundName)
        {
            case "SubmitButton":
                Vm?.Submit();
                e.Handled = true;
                break;

            case "QuickAmountBorder":
                if (found.DataContext is QuickAmountOption option)
                {
                    Vm?.SelectQuickAmount(option.Amount);
                    UpdateQuickAmountSelection(found);
                    e.Handled = true;
                }
                break;

            case "SubPlanBorder":
                if (found.DataContext is SubscriptionPlanOption plan)
                {
                    Vm?.SelectSubscriptionPlan(plan.PatternId);
                    UpdateSubscriptionPlanSelection(found);
                    e.Handled = true;
                }
                break;

            case "CopyProjectIdButton":
                ClipboardHelper.CopyToClipboard(this, Vm?.ProjectId);
                e.Handled = true;
                break;
        }
    }

    /// <summary>Navigate back to project detail (go up one drill-down level).</summary>
    private void NavigateBackToDetail()
    {
        var findProjectsView = this.FindLogicalAncestorOfType<FindProjectsView>();
        if (findProjectsView?.DataContext is FindProjectsViewModel vm)
        {
            vm.CloseInvestPage();
        }
    }

    /// <summary>
    /// Add the invested project to the shared Portfolio ViewModel
    /// so it appears in the "Funded" section.
    /// </summary>
    private static void AddInvestmentToPortfolio(InvestPageViewModel investVm)
    {
        SharedViewModels.Portfolio.AddInvestmentFromProject(
            investVm.Project,
            investVm.FormattedAmount);
    }

    /// <summary>Update quick amount borders via CSS class toggling.
    /// Tracks previously-selected border to avoid full tree walk.</summary>
    private void UpdateQuickAmountSelection(Border newSelected)
    {
        // Deselect previous
        _selectedQuickAmountBorder?.Classes.Set("QuickAmountSelected", false);
        // Select new
        newSelected.Classes.Set("QuickAmountSelected", true);
        _selectedQuickAmountBorder = newSelected;
    }

    /// <summary>Update subscription plan borders via CSS class toggling.
    /// Tracks previously-selected border to avoid full tree walk.</summary>
    private void UpdateSubscriptionPlanSelection(Border newSelected)
    {
        // Deselect previous
        _selectedSubPlanBorder?.Classes.Set("SubPlanSelected", false);
        // Select new
        newSelected.Classes.Set("SubPlanSelected", true);
        _selectedSubPlanBorder = newSelected;
    }
}

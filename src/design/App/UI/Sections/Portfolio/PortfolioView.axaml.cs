using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using App.UI.Shared.Controls;
using App.UI.Shell;
using System.Reactive.Linq;

namespace App.UI.Sections.Portfolio;

public partial class PortfolioView : UserControl
{
    private IDisposable? _visibilitySubscription;

    /// <summary>Design-time only.</summary>
    public PortfolioView() => InitializeComponent();

    public PortfolioView(PortfolioViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);

        // When navigating back to Funded, clear any open detail view
        // so the user sees the list (not a stale detail screen from last time).
        vm.CloseInvestmentDetail();

        // Manage visibility of the portfolio list panel based on ViewModel state
        DataContextChanged += (_, _) => SubscribeToVisibility();
        SubscribeToVisibility();

        // Wire Penalties button to open shell modal
        var penaltiesBtn = this.FindControl<Button>("PenaltiesButton");
        if (penaltiesBtn != null) penaltiesBtn.Click += OnPenaltiesClick;
    }

    private void SubscribeToVisibility()
    {
        _visibilitySubscription?.Dispose();

        if (DataContext is PortfolioViewModel vm)
        {
            vm.ToastRequested -= OnToastRequested;
            _visibilitySubscription = System.Reactive.Disposables.Disposable.Create(() => vm.ToastRequested -= OnToastRequested);
            vm.ToastRequested += OnToastRequested;

            // Portfolio list is visible when: HasInvestments AND no detail selected.
            // HasInvestments is handled by XAML binding; here we also hide when
            // SelectedInvestment is set (drill-down to detail view).
            var visibilitySub = vm.WhenAnyValue(
                x => x.HasInvestments,
                x => x.SelectedInvestment,
                (hasInvestments, selected) => hasInvestments && selected == null)
              .Subscribe(visible =>
              {
                  if (PortfolioListPanel != null)
                      PortfolioListPanel.IsVisible = visible;
              });

            _visibilitySubscription = new System.Reactive.Disposables.CompositeDisposable(_visibilitySubscription, visibilitySub);
        }
    }

    private void OnToastRequested(string message)
    {
        var shellVm = this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;
        shellVm?.ShowToast(message);
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        // Re-subscribe when the cached view is re-added to the tree
        // (the subscription was disposed in OnDetachedFromLogicalTree).
        SubscribeToVisibility();
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _visibilitySubscription?.Dispose();
        _visibilitySubscription = null;
        base.OnDetachedFromLogicalTree(e);
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        switch (btn.Name)
        {
            case "RefreshButton":
                if (DataContext is PortfolioViewModel refreshVm)
                    _ = refreshVm.LoadInvestmentsFromSdkAsync();
                e.Handled = true;
                break;

            case "ManageButton" when btn.Tag is InvestmentViewModel investment:
                if (DataContext is PortfolioViewModel vm)
                    vm.OpenInvestmentDetail(investment);
                break;
        }
    }

    private void OnPenaltiesClick(object? sender, RoutedEventArgs e)
    {
        var shellVm = this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;
        shellVm?.ShowModal(new PenaltiesModal());
    }
}

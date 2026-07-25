using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using App.UI.Shared;

namespace App.UI.Sections.MyProjects.Steps;

public partial class CreateProjectStep4View : UserControl
{
    // ListBox preset controls — Step 4
    private ListBox? _investAmountPresets;
    private ListBox? _fundAmountPresets;
    private ListBox? _subPricePresets;
    private ListBox? _durationPresets;

    // Responsive layout — Fundraising Window dates stack on compact (issue #920)
    private Grid? _fundraisingDatesGrid;
    private StackPanel? _startDatePanel;
    private StackPanel? _endDatePanel;
    private IDisposable? _layoutSubscription;

    public CreateProjectStep4View()
    {
        InitializeComponent();
        SubscribeToLayoutMode();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ResolveNamedElements();
        ApplyResponsiveLayout(LayoutModeService.Instance.IsCompact);
    }

    /// <summary>Idempotent responsive-layout subscription — re-created on every logical-tree attach because OnDetachedFromLogicalTree disposes it (views are cached and re-attached on section switches).</summary>
    private void SubscribeToLayoutMode()
    {
        if (_layoutSubscription != null) return;
        _layoutSubscription = LayoutModeService.Instance
            .WhenAnyValue(x => x.IsCompact)
            .Subscribe(ApplyResponsiveLayout);
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        SubscribeToLayoutMode();
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;
        base.OnDetachedFromLogicalTree(e);
    }

    /// <summary>
    /// Compact: Start/End date columns stack into one column so the pickers and
    /// the month-preset buttons get the full card width (issue #920).
    /// </summary>
    private void ApplyResponsiveLayout(bool isCompact)
    {
        if (_fundraisingDatesGrid == null || _startDatePanel == null || _endDatePanel == null) return;

        if (isCompact)
        {
            _fundraisingDatesGrid.ColumnDefinitions[1].Width = new GridLength(0);
            _fundraisingDatesGrid.ColumnDefinitions[2].Width = new GridLength(0);
            Grid.SetColumn(_endDatePanel, 0);
            Grid.SetRow(_endDatePanel, 1);
            _endDatePanel.Margin = new Thickness(0, 20, 0, 0);
        }
        else
        {
            _fundraisingDatesGrid.ColumnDefinitions[1].Width = new GridLength(24);
            _fundraisingDatesGrid.ColumnDefinitions[2].Width = GridLength.Star;
            Grid.SetColumn(_endDatePanel, 2);
            Grid.SetRow(_endDatePanel, 0);
            _endDatePanel.Margin = new Thickness(0);
        }
    }

    private CreateProjectViewModel? Vm => DataContext as CreateProjectViewModel;

    private void ResolveNamedElements()
    {
        _investAmountPresets = this.FindControl<ListBox>("InvestAmountPresets");
        _fundAmountPresets = this.FindControl<ListBox>("FundAmountPresets");
        _subPricePresets = this.FindControl<ListBox>("SubPricePresets");
        _durationPresets = this.FindControl<ListBox>("DurationPresets");
        _fundraisingDatesGrid = this.FindControl<Grid>("FundraisingDatesGrid");
        _startDatePanel = this.FindControl<StackPanel>("StartDatePanel");
        _endDatePanel = this.FindControl<StackPanel>("EndDatePanel");

        if (_investAmountPresets != null)
            _investAmountPresets.SelectionChanged += (_, _) => OnAmountPresetSelected(_investAmountPresets);
        if (_fundAmountPresets != null)
            _fundAmountPresets.SelectionChanged += (_, _) => OnAmountPresetSelected(_fundAmountPresets);
        if (_subPricePresets != null)
            _subPricePresets.SelectionChanged += (_, _) => OnSubPricePresetSelected(_subPricePresets);
        if (_durationPresets != null)
            _durationPresets.SelectionChanged += (_, _) => OnDurationPresetSelected(_durationPresets);
    }

    #region ListBox Preset Handlers

    /// <summary>
    /// Handle amount preset selection (Investment target amount or Fund goal).
    /// Reads the Tag from the selected ListBoxItem and sets TargetAmount on the VM.
    /// </summary>
    private void OnAmountPresetSelected(ListBox lb)
    {
        if (lb.SelectedItem is not ListBoxItem item) return;
        if (item.Tag is string tag && Vm != null)
        {
            Vm.TargetAmount = tag;
        }
    }

    /// <summary>
    /// Handle subscription price preset selection.
    /// Reads the Tag from the selected ListBoxItem and sets SubscriptionPrice on the VM.
    /// </summary>
    private void OnSubPricePresetSelected(ListBox lb)
    {
        if (lb.SelectedItem is not ListBoxItem item) return;
        if (item.Tag is string tag && Vm != null)
        {
            Vm.SubscriptionPrice = tag;
        }
    }

    /// <summary>
    /// Handle duration preset selection (Investment end date).
    /// Reads the Tag (months as string) and sets the end date relative to now.
    /// </summary>
    private void OnDurationPresetSelected(ListBox lb)
    {
        if (lb.SelectedItem is not ListBoxItem item) return;
        if (item.Tag is string tag && int.TryParse(tag, out var months) && Vm != null)
        {
            Vm.InvestEndDate = DateTime.UtcNow.AddMonths(months);
        }
    }

    #endregion

    /// <summary>
    /// Reset ListBox selections to default state (called by parent on wizard reset).
    /// </summary>
    public void ResetVisualState()
    {
        if (_investAmountPresets != null) _investAmountPresets.SelectedIndex = -1;
        if (_fundAmountPresets != null) _fundAmountPresets.SelectedIndex = -1;
        if (_subPricePresets != null) _subPricePresets.SelectedIndex = -1;
        if (_durationPresets != null) _durationPresets.SelectedIndex = -1;
    }
}

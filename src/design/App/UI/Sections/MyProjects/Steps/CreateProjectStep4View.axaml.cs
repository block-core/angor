using Avalonia.Controls;
using Avalonia.Interactivity;

namespace App.UI.Sections.MyProjects.Steps;

public partial class CreateProjectStep4View : UserControl
{
    // ListBox preset controls — Step 4
    private ListBox? _investAmountPresets;
    private ListBox? _fundAmountPresets;
    private ListBox? _subPricePresets;
    private ListBox? _durationPresets;

    public CreateProjectStep4View()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ResolveNamedElements();
    }

    private CreateProjectViewModel? Vm => DataContext as CreateProjectViewModel;

    private void ResolveNamedElements()
    {
        _investAmountPresets = this.FindControl<ListBox>("InvestAmountPresets");
        _fundAmountPresets = this.FindControl<ListBox>("FundAmountPresets");
        _subPricePresets = this.FindControl<ListBox>("SubPricePresets");
        _durationPresets = this.FindControl<ListBox>("DurationPresets");

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

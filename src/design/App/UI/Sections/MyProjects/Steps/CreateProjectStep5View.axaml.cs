using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using App.UI.Shared;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using ReactiveUI;

namespace App.UI.Sections.MyProjects.Steps;

public partial class CreateProjectStep5View : UserControl
{
    // ListBox controls — Step 5 (Investment)
    private ListBox? _investFrequencyPresets;

    // ListBox controls — Step 5 (Fund/Subscription)
    private Button? _payoutFreqMonthly;
    private Button? _payoutFreqWeekly;
    private Border? _radioOuterMonthly;
    private Border? _radioOuterWeekly;
    private Ellipse? _radioDotMonthly;
    private Ellipse? _radioDotWeekly;
    private TextBlock? _payoutFreqMonthlyText;
    private TextBlock? _payoutFreqWeeklyText;
    private ListBox? _monthlyDateGrid;
    private ListBox? _weeklyDayList;

    // Installment multiselect buttons
    private Button? _installment3;
    private Button? _installment6;
    private Button? _installment9;
    private Border? _check3;
    private Border? _check6;
    private Border? _check9;
    private Control? _checkIcon3;
    private Control? _checkIcon6;
    private Control? _checkIcon9;
    private TextBlock? _installmentText3;
    private TextBlock? _installmentText6;
    private TextBlock? _installmentText9;

    // Track selected duration preset button for CSS class toggling
    private Button? _selectedDurationPresetBtn;

    private IDisposable? _durationValueSubscription;
    private IDisposable? _layoutSubscription;
    private bool _handlersWired;

    public CreateProjectStep5View()
    {
        InitializeComponent();
        SubscribeToLayoutMode();
    }

    /// <summary>Idempotent responsive-layout subscription — re-created on every logical-tree attach because OnDetachedFromLogicalTree disposes it (views are cached and re-attached on section switches).</summary>
    private void SubscribeToLayoutMode()
    {
        if (_layoutSubscription != null) return;
        _layoutSubscription = LayoutModeService.Instance
            .WhenAnyValue(x => x.IsCompact)
            .Subscribe(ApplyResponsiveLayout);
    }

    protected override void OnAttachedToLogicalTree(Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        SubscribeToLayoutMode();
    }

    protected override void OnDetachedFromLogicalTree(Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;
        base.OnDetachedFromLogicalTree(e);
    }

    /// <summary>
    /// Compact: the 4-item frequency row becomes a 2x2 fill grid; desktop keeps a
    /// single full-width row. (Duration presets use BalancedWrapPanel in AXAML at
    /// all breakpoints.)
    /// </summary>
    private void ApplyResponsiveLayout(bool isCompact)
    {
        var freq = this.FindControl<ListBox>("InvestFrequencyPresets");
        if (freq != null)
        {
            freq.Classes.Set("Horizontal", !isCompact);
            freq.Classes.Set("Grid2", isCompact);
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ResolveNamedElements();
        UpdatePayoutFreqVisuals();
        UpdateInstallmentVisuals();
        ApplyResponsiveLayout(LayoutModeService.Instance.IsCompact);

        // Clear duration preset selection when user manually types a non-matching value,
        // or when the duration unit changes (items get regenerated)
        if (Vm != null)
        {
            _durationValueSubscription = Vm.WhenAnyValue(x => x.DurationValue, x => x.DurationUnit)
                .Subscribe(tuple =>
                {
                    var (val, _) = tuple;
                    if (_selectedDurationPresetBtn?.Tag is DurationPresetItem preset
                        && val != preset.Value.ToString())
                    {
                        _selectedDurationPresetBtn.Classes.Set("DurPresetSelected", false);
                        _selectedDurationPresetBtn = null;
                    }
                    else if (_selectedDurationPresetBtn != null
                             && _selectedDurationPresetBtn.Tag is not DurationPresetItem)
                    {
                        // Button was recycled / items regenerated — stale reference
                        _selectedDurationPresetBtn = null;
                    }
                });
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _durationValueSubscription?.Dispose();
        _durationValueSubscription = null;
    }

    private CreateProjectViewModel? Vm => DataContext as CreateProjectViewModel;

    private void ResolveNamedElements()
    {
        // Resolve ListBox controls — Step 5 (Investment)
        _investFrequencyPresets = this.FindControl<ListBox>("InvestFrequencyPresets");

        // Resolve payout frequency buttons — Step 5 (Fund/Subscription)
        _payoutFreqMonthly = this.FindControl<Button>("PayoutFreqMonthly");
        _payoutFreqWeekly = this.FindControl<Button>("PayoutFreqWeekly");
        _radioOuterMonthly = this.FindControl<Border>("RadioOuterMonthly");
        _radioOuterWeekly = this.FindControl<Border>("RadioOuterWeekly");
        _radioDotMonthly = this.FindControl<Ellipse>("RadioDotMonthly");
        _radioDotWeekly = this.FindControl<Ellipse>("RadioDotWeekly");
        _payoutFreqMonthlyText = this.FindControl<TextBlock>("PayoutFreqMonthlyText");
        _payoutFreqWeeklyText = this.FindControl<TextBlock>("PayoutFreqWeeklyText");
        _monthlyDateGrid = this.FindControl<ListBox>("MonthlyDateGrid");
        _weeklyDayList = this.FindControl<ListBox>("WeeklyDayList");

        // Resolve installment multiselect buttons
        _installment3 = this.FindControl<Button>("Installment3");
        _installment6 = this.FindControl<Button>("Installment6");
        _installment9 = this.FindControl<Button>("Installment9");
        _check3 = this.FindControl<Border>("Check3");
        _check6 = this.FindControl<Border>("Check6");
        _check9 = this.FindControl<Border>("Check9");
        _checkIcon3 = this.FindControl<Control>("CheckIcon3");
        _checkIcon6 = this.FindControl<Control>("CheckIcon6");
        _checkIcon9 = this.FindControl<Control>("CheckIcon9");
        _installmentText3 = this.FindControl<TextBlock>("InstallmentText3");
        _installmentText6 = this.FindControl<TextBlock>("InstallmentText6");
        _installmentText9 = this.FindControl<TextBlock>("InstallmentText9");

        if (_handlersWired)
            return;

        // Wire up Step 5 ListBox selection changed handlers (Investment)
        if (_investFrequencyPresets != null)
            _investFrequencyPresets.SelectionChanged += (_, _) => OnInvestFrequencySelected();

        // Wire up Step 5 payout frequency click handlers (Fund/Subscription)
        WirePayoutFreqBorder(_payoutFreqMonthly, "Monthly");
        WirePayoutFreqBorder(_payoutFreqWeekly, "Weekly");
        if (_monthlyDateGrid != null)
            _monthlyDateGrid.SelectionChanged += (_, _) => OnMonthlyDateSelected();
        if (_weeklyDayList != null)
            _weeklyDayList.SelectionChanged += (_, _) => OnWeeklyDaySelected();

        // Wire up installment multiselect click handlers
        WireInstallmentBorder(_installment3, 3);
        WireInstallmentBorder(_installment6, 6);
        WireInstallmentBorder(_installment9, 9);

        _handlersWired = true;
    }

    #region Duration Preset Handler

    /// <summary>
    /// Handle dynamic duration preset button click (Step 5 Investment).
    /// The button's Tag contains the preset value (int), DataContext is the bound int from DurationPresetItems.
    /// Toggles DurPresetSelected CSS class on the clicked button.
    /// </summary>
    private void OnDurationPresetClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DurationPresetItem preset && Vm != null)
        {
            // Deselect previous
            _selectedDurationPresetBtn?.Classes.Set("DurPresetSelected", false);

            // Select new
            _selectedDurationPresetBtn = btn;
            btn.Classes.Set("DurPresetSelected", true);

            Vm.DurationPreset = preset.Value;
        }
    }

    #endregion

    #region Investment Frequency Handler

    /// <summary>
    /// Handle Investment release frequency selection on Step 5.
    /// Sets ReleaseFrequency on the VM.
    /// </summary>
    private void OnInvestFrequencySelected()
    {
        if (_investFrequencyPresets?.SelectedItem is not ListBoxItem item) return;
        if (item.Tag is string tag && Vm != null)
        {
            Vm.ReleaseFrequency = tag;
        }
    }

    #endregion

    #region Payout Frequency Handlers

    /// <summary>
    /// Handle Fund/Sub payout frequency selection on Step 5.
    /// Sets PayoutFrequency ("Monthly" or "Weekly") on the VM.
    /// Uses manual Border elements with radio indicator (like installments pattern).
    /// </summary>
    private void WirePayoutFreqBorder(Button? button, string frequency)
    {
        if (button == null) return;
        button.Click += (_, _) =>
        {
            if (Vm != null)
            {
                Vm.PayoutFrequency = frequency;
                UpdatePayoutFreqVisuals();
            }
        };
    }

    /// <summary>
    /// Update payout frequency radio visuals based on VM.PayoutFrequency.
    /// Vue: 20x20 rounded-full border-2, with 12x12 green filled circle inside when active.
    /// </summary>
    private void UpdatePayoutFreqVisuals()
    {
        if (Vm == null) return;
        var freq = Vm.PayoutFrequency;
        var isMonthly = freq == "Monthly";
        var isWeekly = freq == "Weekly";

        // Row border + bg
        _payoutFreqMonthly?.Classes.Set("PayoutFreqSelected", isMonthly);
        _payoutFreqWeekly?.Classes.Set("PayoutFreqSelected", isWeekly);

        // Radio outer ring
        _radioOuterMonthly?.Classes.Set("RadioSelected", isMonthly);
        _radioOuterWeekly?.Classes.Set("RadioSelected", isWeekly);

        // Radio inner dot visibility
        if (_radioDotMonthly != null) _radioDotMonthly.IsVisible = isMonthly;
        if (_radioDotWeekly != null) _radioDotWeekly.IsVisible = isWeekly;

        // Text color: green when selected
        _payoutFreqMonthlyText?.Classes.Set("PayoutFreqTextSelected", isMonthly);
        _payoutFreqWeeklyText?.Classes.Set("PayoutFreqTextSelected", isWeekly);
    }

    #endregion

    #region Installment Handlers

    /// <summary>
    /// Wire a single installment border for multiselect click toggling.
    /// Vue: toggleInstallmentCount() — toggle count in/out of installmentCounts array.
    /// </summary>
    private void WireInstallmentBorder(Button? button, int count)
    {
        if (button == null) return;
        button.Click += (_, _) =>
        {
            Vm?.ToggleInstallmentCount(count);
            UpdateInstallmentVisuals();
        };
    }

    /// <summary>
    /// Update installment checkbox visuals based on SelectedInstallmentCounts.
    /// Vue: .settings-toggle-button.active → green bg + white checkmark.
    /// </summary>
    private void UpdateInstallmentVisuals()
    {
        if (Vm == null) return;
        UpdateSingleInstallment(3, _installment3, _check3, _checkIcon3, _installmentText3);
        UpdateSingleInstallment(6, _installment6, _check6, _checkIcon6, _installmentText6);
        UpdateSingleInstallment(9, _installment9, _check9, _checkIcon9, _installmentText9);
    }

    private void UpdateSingleInstallment(int count, Button? row, Border? checkBorder, Control? checkIcon, TextBlock? text)
    {
        if (Vm == null) return;
        var isSelected = Vm.SelectedInstallmentCounts.Contains(count);

        // Row border + bg
        row?.Classes.Set("InstallmentSelected", isSelected);

        // Checkbox: green bg + green border when selected, transparent otherwise
        if (checkBorder != null)
        {
            checkBorder.Classes.Set("CheckboxActive", isSelected);
        }

        // Checkmark icon visibility
        if (checkIcon != null)
            checkIcon.IsVisible = isSelected;

        // Text color: green when selected (Vue: text-[#5FAF78] dark / text-[#4B7C5A] light)
        text?.Classes.Set("InstallmentTextSelected", isSelected);
    }

    #endregion

    #region Day Picker Handlers

    /// <summary>
    /// Handle Fund/Sub monthly payout date selection on Step 5.
    /// Sets MonthlyPayoutDate (1-29) on the VM.
    /// </summary>
    private void OnMonthlyDateSelected()
    {
        if (_monthlyDateGrid?.SelectedItem is not ListBoxItem item) return;
        if (item.Tag is string tag && int.TryParse(tag, out var day) && Vm != null)
        {
            Vm.MonthlyPayoutDate = day;
        }
    }

    /// <summary>
    /// Handle Fund/Sub weekly payout day selection on Step 5.
    /// Sets WeeklyPayoutDay ("Mon".."Sun") on the VM.
    /// </summary>
    private void OnWeeklyDaySelected()
    {
        if (_weeklyDayList?.SelectedItem is not ListBoxItem item) return;
        if (item.Tag is string tag && Vm != null)
        {
            Vm.WeeklyPayoutDay = tag;
        }
    }

    #endregion

    /// <summary>
    /// Reset all Step 5 visual state (called by parent on wizard reset).
    /// </summary>
    public void ResetVisualState()
    {
        // Clear ListBox selections (Step 5 - Investment)
        if (_investFrequencyPresets != null) _investFrequencyPresets.SelectedIndex = -1;

        // Clear duration preset button selection (Step 5 - Investment)
        _selectedDurationPresetBtn?.Classes.Set("DurPresetSelected", false);
        _selectedDurationPresetBtn = null;

        // Clear ListBox selections (Step 5 - Fund/Sub)
        if (_monthlyDateGrid != null) _monthlyDateGrid.SelectedIndex = -1;
        if (_weeklyDayList != null) _weeklyDayList.SelectedIndex = -1;

        // Reset payout frequency visuals
        UpdatePayoutFreqVisuals();

        // Reset installment visuals
        UpdateInstallmentVisuals();
    }
}

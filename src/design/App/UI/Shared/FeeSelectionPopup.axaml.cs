using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using App.UI.Shell;

namespace App.UI.Shared;

/// <summary>
/// Reusable fee selection popup modal.
/// Shows 3 preset fee rates (Priority=50, Standard=20, Economy=5 sat/vB),
/// an optional custom fee rate input, and Cancel / Confirm buttons.
/// Callers await the result via <see cref="ShowAsync"/>.
/// Implements IBackdropCloseable for shell backdrop click handling.
/// </summary>
public partial class FeeSelectionPopup : UserControl, IBackdropCloseable
{
    private readonly TaskCompletionSource<long?> _tcs = new();

    /// <summary>
    /// Currently selected preset: "priority" (50), "standard" (20), "economy" (5).
    /// Null when custom fee is active.
    /// </summary>
    private string _selectedPreset = "standard";

    public FeeSelectionPopup()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);
        AddHandler(Border.PointerPressedEvent, OnBorderPressed, RoutingStrategies.Bubble);

        // Toggle custom fee panel visibility when checkbox changes
        CustomFeeCheckbox.IsCheckedChanged += (_, _) =>
        {
            var isCustom = CustomFeeCheckbox.IsChecked == true;
            CustomFeePanel.IsVisible = isCustom;

            if (isCustom)
            {
                // Deselect all presets when switching to custom
                _selectedPreset = "";
                UpdatePresetVisuals();
                CustomFeeInput.Focus();
            }
            else
            {
                // Revert to standard when unchecking custom
                _selectedPreset = "standard";
                UpdatePresetVisuals();
                CustomFeeError.IsVisible = false;
            }
        };
    }

    /// <summary>
    /// The task that callers await to get the selected fee rate (or null on cancel).
    /// </summary>
    public Task<long?> Result => _tcs.Task;

    /// <summary>
    /// Shows the fee selection popup as a shell modal and returns the selected fee rate.
    /// Returns null if the user cancels.
    /// </summary>
    /// <param name="shellVm">The shell ViewModel to show the modal on.</param>
    /// <returns>Selected fee rate in sat/vB, or null if cancelled.</returns>
    public static async Task<long?> ShowAsync(ShellViewModel shellVm)
    {
        var popup = new FeeSelectionPopup();
        shellVm.ShowModal(popup);
        var result = await popup.Result;
        shellVm.HideModal();
        return result;
    }

    /// <summary>
    /// Called by the shell when the backdrop is clicked.
    /// Cancels the selection (returns null).
    /// </summary>
    public void OnBackdropCloseRequested()
    {
        _tcs.TrySetResult(null);
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        switch (btn.Name)
        {
            case "CloseButton":
            case "CancelButton":
                _tcs.TrySetResult(null);
                break;

            case "ConfirmButton":
                Confirm();
                break;
        }
    }

    private void Confirm()
    {
        if (CustomFeeCheckbox.IsChecked == true)
        {
            // Validate custom fee input
            var text = CustomFeeInput.Text?.Trim() ?? "";
            if (!long.TryParse(text, out var customRate) || customRate < 1)
            {
                CustomFeeError.Text = "Enter a valid fee rate (minimum 1 sat/vB)";
                CustomFeeError.IsVisible = true;
                return;
            }

            if (customRate > 10000)
            {
                CustomFeeError.Text = "Fee rate seems too high (max 10,000 sat/vB)";
                CustomFeeError.IsVisible = true;
                return;
            }

            _tcs.TrySetResult(customRate);
        }
        else
        {
            var rate = _selectedPreset switch
            {
                "priority" => 50L,
                "economy" => 5L,
                _ => 20L // standard
            };
            _tcs.TrySetResult(rate);
        }
    }

    // ── Fee preset border selection handling ──

    private void OnBorderPressed(object? sender, PointerPressedEventArgs e)
    {
        var source = e.Source as Control;

        // Walk up the tree to find a named fee border
        while (source != null)
        {
            if (source is Border b && !string.IsNullOrEmpty(b.Name))
            {
                var name = b.Name;
                if (name is "FeePriority" or "FeeStandard" or "FeeEconomy")
                {
                    _selectedPreset = name switch
                    {
                        "FeePriority" => "priority",
                        "FeeStandard" => "standard",
                        "FeeEconomy" => "economy",
                        _ => "standard"
                    };

                    // Uncheck custom checkbox if a preset is selected
                    if (CustomFeeCheckbox.IsChecked == true)
                    {
                        CustomFeeCheckbox.IsChecked = false;
                        CustomFeePanel.IsVisible = false;
                        CustomFeeError.IsVisible = false;
                    }

                    UpdatePresetVisuals();
                    e.Handled = true;
                    return;
                }
            }
            source = source.Parent as Control;
        }
    }

    /// <summary>
    /// Updates the FeeSelected CSS class and text foreground colors for all 3 presets.
    /// Per Rule #9: no BrushTransition — instant state changes only.
    /// </summary>
    private void UpdatePresetVisuals()
    {
        SetFeeSelection(FeePriority, _selectedPreset == "priority",
            "FeePriorityLabel", "FeePriorityDesc", "FeePriorityRate");
        SetFeeSelection(FeeStandard, _selectedPreset == "standard",
            "FeeStandardLabel", "FeeStandardDesc", "FeeStandardRate");
        SetFeeSelection(FeeEconomy, _selectedPreset == "economy",
            "FeeEconomyLabel", "FeeEconomyDesc", "FeeEconomyRate");
    }

    private void SetFeeSelection(Border? border, bool isSelected,
        string labelName, string descName, string rateName)
    {
        if (border == null) return;

        border.Classes.Set("FeeSelected", isSelected);

        var selectedFg = Brushes.White;
        var unselectedFg = this.TryFindResource("RecoveryFeeUnselectedText", out var res) && res is IBrush brush
            ? brush
            : Brushes.Gray;

        var fg = isSelected ? selectedFg : unselectedFg;

        SetTextForeground(labelName, fg);
        SetTextForeground(descName, fg);
        SetTextForeground(rateName, fg);
    }

    private void SetTextForeground(string name, IBrush fg)
    {
        var tb = this.FindControl<TextBlock>(name);
        if (tb != null) tb.Foreground = fg;
    }
}

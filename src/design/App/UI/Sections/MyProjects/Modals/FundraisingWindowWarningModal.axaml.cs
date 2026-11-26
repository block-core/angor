using Avalonia.Controls;
using App.UI.Shell;

namespace App.UI.Sections.MyProjects.Modals;

/// <summary>
/// Confirm/cancel warning shown when a user sets the fundraising window
/// ("Fundraising Close") to more than 3 months \u2014 commonly mistaken for the
/// project's overall duration, which is actually set later via Stages.
/// </summary>
public partial class FundraisingWindowWarningModal : UserControl, IBackdropCloseable
{
    private readonly Action? _onCancel;

    /// <summary>Parameterless ctor for XAML designer.</summary>
    public FundraisingWindowWarningModal() : this("3 months", null, null) { }

    public FundraisingWindowWarningModal(string durationText, Action? onConfirm, Action? onCancel)
    {
        _onCancel = onCancel;
        InitializeComponent();

        var questionText = this.FindControl<TextBlock>("QuestionText");
        if (questionText != null)
            questionText.Text = $"Are you sure you want to fundraise for {durationText}?";

        var yesButton = this.FindControl<Button>("YesButton");
        if (yesButton != null) yesButton.Click += (_, _) => onConfirm?.Invoke();

        var noButton = this.FindControl<Button>("NoButton");
        if (noButton != null) noButton.Click += (_, _) => onCancel?.Invoke();

        var closeButton = this.FindControl<Button>("CloseButton");
        if (closeButton != null) closeButton.Click += (_, _) => onCancel?.Invoke();
    }

    /// <summary>Clicking the backdrop is treated the same as "No" \u2014 the safer default.</summary>
    public void OnBackdropCloseRequested() => _onCancel?.Invoke();
}

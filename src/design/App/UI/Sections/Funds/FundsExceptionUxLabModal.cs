#if DEBUG
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using App.UI.Shared;
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace App.UI.Sections.Funds;

/// <summary>
/// Debug-only Exception UX Lab for the Funds section (DESIGN-FUNDS-*). Previews the
/// real surfaces each case uses today: toasts (faucet / refresh / create wallet) and
/// the Send Funds modal (validation, broadcast failure, success). No SDK call is made.
/// </summary>
public sealed class FundsExceptionUxLabModal : UserControl, IBackdropCloseable
{
    private readonly ShellViewModel shellVm;

    public FundsExceptionUxLabModal(ShellViewModel shellVm)
    {
        this.shellVm = shellVm;
        Content = BuildContent();
        this.shellVm.PropertyChanged += OnShellPropertyChanged;
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        shellVm.PropertyChanged -= OnShellPropertyChanged;
        base.OnDetachedFromVisualTree(e);
    }

    public void OnBackdropCloseRequested() => shellVm.HideModal();

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.IsDarkThemeEnabled))
        {
            Content = BuildContent();
        }
    }

    private Control BuildContent()
    {
        Border root = new()
        {
            Classes = { "ModalCard", "Wide" },
            Background = GetBrush("DeployModalBackground"),
            BorderBrush = GetBrush("DeployModalBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(24),
            MaxHeight = 760
        };

        StackPanel content = new() { Spacing = 16 };
        content.Children.Add(new TextBlock
        {
            Text = "Funds UX Lab",
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            Foreground = GetBrush("TextStrong")
        });
        content.Children.Add(new TextBlock
        {
            Text = "Preview the real surface each Funds case uses today. Toast cases show the proposed wording; Send cases open the Send Funds modal. No SDK call is made.",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Foreground = GetBrush("TextMuted")
        });

        foreach (FundsLabCase labCase in FundsLabCase.All)
        {
            content.Children.Add(BuildCaseRow(labCase));
        }

        Button closeButton = CreateButton("Close", () => shellVm.HideModal());
        closeButton.HorizontalAlignment = HorizontalAlignment.Right;
        content.Children.Add(closeButton);

        root.Child = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };

        return root;
    }

    private Control BuildCaseRow(FundsLabCase labCase)
    {
        Border card = new()
        {
            Background = GetBrush("RecoveryInfoCardBg"),
            BorderBrush = GetBrush("RecoveryInfoCardBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16)
        };

        Grid grid = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        StackPanel copy = new() { Spacing = 4 };
        copy.Children.Add(new TextBlock
        {
            Text = labCase.Title,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = GetBrush("TextStrong")
        });
        copy.Children.Add(new TextBlock
        {
            Text = labCase.Message,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = GetBrush("TextMuted")
        });

        Button previewButton = CreateButton(labCase.IsToast ? "Show toast" : "Open modal", () => Run(labCase));
        previewButton.VerticalAlignment = VerticalAlignment.Center;

        Grid.SetColumn(copy, 0);
        Grid.SetColumn(previewButton, 1);
        grid.Children.Add(copy);
        grid.Children.Add(previewButton);
        card.Child = grid;
        return card;
    }

    private Button CreateButton(string text, Action onClick)
    {
        Button button = new()
        {
            Content = text,
            Padding = new Thickness(14, 8),
            CornerRadius = new CornerRadius(8),
            Background = GetBrush("ModalCancelBg"),
            Foreground = GetBrush("ModalCancelFg"),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private void Run(FundsLabCase labCase)
    {
        if (labCase.IsToast)
        {
            shellVm.ShowToast(labCase.ToastMessage!, labCase.Severity);
            return;
        }

        var fundsVm = global::App.App.Services.GetRequiredService<FundsViewModel>();
        var modal = new SendFundsModal { DataContext = fundsVm };
        modal.ShowLabState(labCase.SendMode!, labCase.SendError);
        shellVm.ShowModal(modal);
    }

    private IBrush GetBrush(string resourceKey)
    {
        if (Application.Current?.TryGetResource(resourceKey, Application.Current.ActualThemeVariant, out object? resource) == true &&
            resource is IBrush brush)
        {
            return brush;
        }

        if (Application.Current?.TryGetResource(resourceKey, Application.Current.ActualThemeVariant, out resource) == true &&
            resource is Color color)
        {
            return new SolidColorBrush(color);
        }

        return Brushes.Transparent;
    }

    private sealed record FundsLabCase(
        string Title,
        string Message,
        bool IsToast,
        string? ToastMessage,
        ToastSeverity Severity,
        string? SendMode,
        string? SendError)
    {
        public static readonly IReadOnlyList<FundsLabCase> All =
        [
            new("Faucet — limit reached", "DESIGN-FUNDS-003 — wallet already has enough test coins. Should read as a neutral/warning, not a failure.",
                true, "Your test wallet already has enough coins. Try spending some before requesting more.", ToastSeverity.Warning, null, null),
            new("Faucet — service unavailable", "DESIGN-FUNDS-003 — faucet rejected or HTTP error (raw \"Faucet failed: {reason}\" today).",
                true, "The testnet faucet is unavailable right now. Please try again in a few minutes.", ToastSeverity.Error, null, null),
            new("Faucet — success", "DESIGN-FUNDS-003 success — coins requested.",
                true, "Testnet coins are on the way. Your balance will update shortly.", ToastSeverity.Success, null, null),
            new("Refresh balance failed", "DESIGN-FUNDS-002 — refresh threw (raw \"Failed to refresh balance: {ex}\" today). Should be non-blocking.",
                true, "We couldn't refresh this wallet's balance. It will retry automatically — your funds are unaffected.", ToastSeverity.Warning, null, null),
            new("Create wallet failed", "DESIGN-FUNDS-001 — wallet create/restore failed (raw error today).",
                true, "We couldn't create the wallet. Please try again.", ToastSeverity.Error, null, null),
            new("Send — validation error", "DESIGN-FUNDS-004 inline — field-level validation on the amount/address.",
                false, null, ToastSeverity.Success, "validation", null),
            new("Send — broadcast failed", "DESIGN-FUNDS-004 — today the raw service error is shown in the Amount field (wrong place). Decide a dedicated send-error banner.",
                false, null, ToastSeverity.Success, "error", "We couldn't broadcast this transaction. The network rejected it — check your connection and try again."),
            new("Send — success", "DESIGN-FUNDS-005 — broadcast succeeded; shows txid with copy / explorer actions.",
                false, null, ToastSeverity.Success, "success", null),
        ];
    }
}
#endif

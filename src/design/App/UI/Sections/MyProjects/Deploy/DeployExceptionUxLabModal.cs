#if DEBUG
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using App.UI.Shell;

namespace App.UI.Sections.MyProjects.Deploy;

public sealed class DeployExceptionUxLabModal : UserControl, IBackdropCloseable
{
    private readonly ShellViewModel shellVm;
    private readonly DeployFlowViewModel deployVm;

    public DeployExceptionUxLabModal(ShellViewModel shellVm, DeployFlowViewModel deployVm)
    {
        this.shellVm = shellVm;
        this.deployVm = deployVm;
        Content = BuildContent();
        this.shellVm.PropertyChanged += OnShellPropertyChanged;
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        shellVm.PropertyChanged -= OnShellPropertyChanged;
        base.OnDetachedFromVisualTree(e);
    }

    public void OnBackdropCloseRequested()
    {
        shellVm.HideModal();
    }

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
            Text = "Deploy UX Lab",
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            Foreground = GetBrush("TextStrong")
        });
        content.Children.Add(new TextBlock
        {
            Text = "Pick a deploy case to preview the real deploy overlay state. No SDK calls are made.",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Foreground = GetBrush("TextMuted")
        });

        foreach (DeployLabCase labCase in DeployLabCase.All)
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

    private Control BuildCaseRow(DeployLabCase labCase)
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

        Button previewButton = CreateButton("Preview", () => OpenDeployPreview(labCase));
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

    private void OpenDeployPreview(DeployLabCase labCase)
    {
        deployVm.ShowLabPreview(
            labCase.Screen,
            "Exception UX Preview Project",
            labCase.StatusText,
            labCase.ErrorText,
            labCase.IsActionBlocked);

        shellVm.ShowModal(new DeployFlowOverlay { DataContext = deployVm });
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

    private sealed record DeployLabCase(
        string Title,
        DeployScreen Screen,
        string StatusText,
        string? ErrorText,
        string Message,
        bool IsActionBlocked)
    {
        public static readonly IReadOnlyList<DeployLabCase> All =
        [
            new("Missing project data", DeployScreen.WalletSelector, "Project data is missing.", "We could not start deployment because the project draft was not available. Go back to Review & Deploy and try again.", "The deploy flow opened without the project data it needs.", true),
            new("Project keys failed", DeployScreen.WalletSelector, "Project key setup failed.", "We could not reserve project keys for this wallet. Refresh My Projects, unlock the wallet, and try again.", "The app cannot create founder project keys.", true),
            new("Nostr profile failed", DeployScreen.WalletSelector, "Profile publishing failed.", "We could not publish the project profile to Nostr relays. Check your relay connection and try again.", "The profile event failed or timed out.", false),
            new("Project info failed", DeployScreen.WalletSelector, "Project info publishing failed.", "We could not publish the project details to Nostr relays. Review the project details and try again.", "The project info event failed validation, timed out, or was rejected.", false),
            new("Transaction build failed", DeployScreen.WalletSelector, "Transaction preparation failed.", "We could not prepare the on-chain deployment transaction. Check wallet balance, fee rate, and spendable funds.", "The Bitcoin transaction draft could not be built.", true),
            new("Broadcast failed", DeployScreen.WalletSelector, "Broadcast failed.", "The deployment transaction was prepared, but the network rejected broadcast. Try again or inspect technical details.", "The indexer/network rejected the transaction broadcast.", false),
            new("Unexpected deploy exception", DeployScreen.WalletSelector, "Deployment failed.", "Something unexpected happened while deploying. No funds were moved by this preview. Try again or view technical details.", "A code exception occurred during deploy.", false),
            new("Invoice has no wallet", DeployScreen.PayFee, "Wallet unavailable.", "We cannot watch this invoice payment because no wallet is available. Create or unlock a wallet, then try again.", "Invoice monitoring has no wallet to derive keys or watch payment.", true),
            new("Invoice monitoring cancelled", DeployScreen.PayFee, "Invoice monitoring cancelled.", null, "Cancellation should be treated as neutral/info, not an error.", false),
            new("Deploy success", DeployScreen.Success, "Project deployed.", null, "Deployment completed successfully.", false)
        ];
    }
}
#endif

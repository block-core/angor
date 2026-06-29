#if DEBUG
using System.ComponentModel;
using System.Threading.Tasks;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Wallet.Application;
using Angor.Sdk.Wallet.Domain;
using Angor.Shared.Integration.Lightning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using App.UI.Shared;
using App.UI.Shared.Services;
using App.UI.Shell;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Lives in the PaymentFlow namespace so NetworkTab/PaymentFlowScreen resolve to the
// payment-flow types (the FindProjects namespace declares a different NetworkTab).
namespace App.UI.Shared.PaymentFlow;

/// <summary>
/// Debug-only Exception UX Lab for the shared payment flow (DESIGN-PAY-*).
/// Builds a local PaymentFlowViewModel from DI with no-op callbacks and previews
/// each failure/intermediate/success state in the real PaymentFlowView. No SDK
/// transaction is ever sent — previews set display state directly.
/// </summary>
public sealed class PaymentExceptionUxLabModal : UserControl, IBackdropCloseable
{
    private readonly ShellViewModel shellVm;
    private readonly PaymentFlowViewModel paymentVm;

    public PaymentExceptionUxLabModal(ShellViewModel shellVm)
    {
        this.shellVm = shellVm;
        this.paymentVm = BuildPreviewViewModel();
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

    private static PaymentFlowViewModel BuildPreviewViewModel()
    {
        var sp = global::App.App.Services;
        var config = new PaymentFlowConfig
        {
            AmountSats = 125_000,
            StageCount = 3,
            FeeRateSatsPerVbyte = 20,
            Title = "Pay to Invest",
            SuccessTitle = "Investment Submitted",
            SuccessDescription = "Your investment of 0.00125000 TBTC has been submitted to the network and is now pending confirmation.",
            SuccessButtonText = "View My Investments",
            OnSuccessButtonClicked = () => { },
            OnPaymentReceived = (_, _, _) => Task.FromResult(Result.Success()),
            OnPayWithWallet = (_, _, _) => Task.FromResult(Result.Success()),
            SkipWalletSelectorWhenNoWalletCanPay = false,
        };

        return new PaymentFlowViewModel(
            sp.GetRequiredService<IWalletAppService>(),
            sp.GetRequiredService<IInvestmentAppService>(),
            sp.GetRequiredService<IBoltzSwapService>(),
            sp.GetRequiredService<IBoltzSwapStorageService>(),
            sp.GetRequiredService<IWalletContext>(),
            sp.GetRequiredService<ICurrencyService>(),
            sp.GetRequiredService<System.Func<BitcoinNetwork>>(),
            sp.GetRequiredService<ILogger<PaymentFlowViewModel>>(),
            sp.GetRequiredService<PrototypeSettings>(),
            config);
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
            Text = "Payment UX Lab",
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            Foreground = GetBrush("TextStrong")
        });
        content.Children.Add(new TextBlock
        {
            Text = "Pick a payment case to preview the real payment overlay state. No SDK transaction is sent.",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Foreground = GetBrush("TextMuted")
        });

        foreach (PaymentLabCase labCase in PaymentLabCase.All)
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

    private Control BuildCaseRow(PaymentLabCase labCase)
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

        Button previewButton = CreateButton("Preview", () => OpenPreview(labCase));
        previewButton.VerticalAlignment = VerticalAlignment.Center;

        Grid.SetColumn(copy, 0);
        Grid.SetColumn(previewButton, 1);
        grid.Children.Add(copy);
        grid.Children.Add(previewButton);
        card.Child = grid;
        return card;
    }

    private Button CreateButton(string text, System.Action onClick)
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

    private void OpenPreview(PaymentLabCase labCase)
    {
        paymentVm.ShowLabPreview(
            labCase.Screen,
            labCase.Tab,
            labCase.StatusText,
            labCase.Error,
            labCase.PaymentReceived,
            labCase.IsProcessing);

        shellVm.ShowModal(new PaymentFlowView { DataContext = paymentVm });
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

    private sealed record PaymentLabCase(
        string Title,
        PaymentFlowScreen Screen,
        NetworkTab Tab,
        string StatusText,
        string? Error,
        bool PaymentReceived,
        bool IsProcessing,
        string Message)
    {
        public static readonly System.Collections.Generic.IReadOnlyList<PaymentLabCase> All =
        [
            new("Direct wallet payment failed", PaymentFlowScreen.WalletSelector, NetworkTab.OnChain,
                "Awaiting payment...",
                "We could not complete the payment from this wallet. It may not have enough confirmed funds — add funds or choose another wallet.",
                false, false,
                "DESIGN-PAY-001 — direct wallet callback returned a failure (raw service error today)."),
            new("Direct wallet payment threw", PaymentFlowScreen.WalletSelector, NetworkTab.OnChain,
                "Awaiting payment...",
                "Payment could not be processed. The wallet could not sign the transaction — unlock it and try again.",
                false, false,
                "DESIGN-PAY-002 — direct wallet callback threw (raw \"Payment failed: {ex}\" today)."),
            new("Auto wallet creation failed", PaymentFlowScreen.Invoice, NetworkTab.OnChain,
                "Creating wallet...",
                "We could not set up a wallet to receive this payment. Try again, or create a wallet first from the Funds tab.",
                false, false,
                "DESIGN-PAY-003 — auto-create/reload failed (raw SDK / \"not found after reload\" / \"no ID\")."),
            new("Receive address failed", PaymentFlowScreen.Invoice, NetworkTab.OnChain,
                "Generating invoice address...",
                "We could not prepare a receive address for this payment. Unlock the wallet and try again.",
                false, false,
                "DESIGN-PAY-004 — refresh / GetNextReceiveAddress failure (leaks method names today)."),
            new("On-chain monitoring failed", PaymentFlowScreen.Invoice, NetworkTab.OnChain,
                "Waiting for payment...",
                "We stopped watching for this payment because the connection to the network failed. Your funds are safe — reopen to resume watching.",
                false, false,
                "DESIGN-PAY-005 — monitor setup/timeout/failure (mixed technical wording today)."),
            new("Lightning setup failed", PaymentFlowScreen.Invoice, NetworkTab.Lightning,
                "Creating Lightning invoice...",
                "We could not create the Lightning invoice. You can switch to On-Chain to pay instead, or try Lightning again.",
                false, false,
                "DESIGN-PAY-006 — claim key / CreateLightningSwap failure (raw \"threw\"/\"failed\" today)."),
            new("Lightning confirmation failed", PaymentFlowScreen.Invoice, NetworkTab.Lightning,
                "Waiting for Lightning payment...",
                "The Lightning payment could not be confirmed on-chain. If you already paid, your funds are safe and will be claimed automatically.",
                false, false,
                "DESIGN-PAY-007 — swap/claim monitor failure after invoice created."),
            new("Payment received, finalization failed", PaymentFlowScreen.Invoice, NetworkTab.OnChain,
                "Processing...",
                "Payment was received, but we could not finish submitting your transaction. Do not pay again — retry finalization or contact support.",
                true, false,
                "DESIGN-PAY-008 — post-payment callback failed (serious: funds in, action out)."),
            new("Payment detected (intermediate)", PaymentFlowScreen.Invoice, NetworkTab.OnChain,
                "Payment received!",
                null,
                true, true,
                "DESIGN-PAY-009 — \"Payment received\" / \"Processing\" should read as intermediate, not final."),
            new("Payment success", PaymentFlowScreen.Success, NetworkTab.OnChain,
                "Payment received!",
                null,
                true, false,
                "Final success state for comparison."),
        ];
    }
}
#endif

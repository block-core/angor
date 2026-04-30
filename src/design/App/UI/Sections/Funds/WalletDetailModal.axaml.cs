using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Angor.Shared.Models;
using App.UI.Shared;
using App.UI.Shared.Helpers;
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Projektanker.Icons.Avalonia;

namespace App.UI.Sections.Funds;

/// <summary>
/// Wallet Detail / UTXO Management Modal — Vue Funds.vue wallet detail:
///   Sticky header: wallet icon + name/type, balance + Send/Receive buttons
///   Body: UTXO list with selectable cards (txid, amount, confirmations, checkbox)
///   Selected UTXOs update Send button text: "Send" / "Send 1 UTXO" / "Send N UTXOs"
///
/// DataContext = FundsViewModel (set by FundsView when opening).
/// Wallet info set via SetWallet() before showing.
///
/// UTXO card and checkbox colors use XAML styles with DynamicResource (Rule #9).
/// Code-behind only toggles CSS classes — zero color logic.
/// </summary>
public partial class WalletDetailModal : UserControl, IBackdropCloseable
{
    private readonly ILogger<WalletDetailModal> _logger;
    private string _walletName = "";
    private string _walletType = "";
    private string _walletBalance = "";
    private string _walletId = "";
    private readonly HashSet<string> _selectedUtxos = new();
    private List<UtxoData> _utxos = new();

    private ICurrencyService CurrencyService =>
        App.Services.GetRequiredService<ICurrencyService>();

    public WalletDetailModal()
    {
        InitializeComponent();
        _logger = App.Services.GetRequiredService<ILoggerFactory>().CreateLogger<WalletDetailModal>();
        AddHandler(Button.ClickEvent, OnButtonClick);
    }

    private ShellViewModel? ShellVm =>
        this.FindAncestorOfType<ShellView>()?.DataContext as ShellViewModel;

    public void OnBackdropCloseRequested() { }

    /// <summary>
    /// Set the wallet info and populate UTXOs from SDK data.
    /// Called by FundsView before showing the modal.
    /// </summary>
    public void SetWallet(string name, string type, string balance, string walletId)
    {
        _walletName = name;
        _walletType = type;
        _walletBalance = balance;
        _walletId = walletId;

        HeaderWalletName.Text = name;
        HeaderWalletType.Text = type;
        HeaderBalance.Text = balance;
        _selectedUtxos.Clear();

        // Load real UTXOs from the FundsViewModel's cached AccountBalanceInfo
        _utxos = new List<UtxoData>();
        if (DataContext is FundsViewModel fundsVm && !string.IsNullOrEmpty(walletId))
        {
            // Refresh UTXO cache from SDK, then load
            _ = LoadUtxosAsync(fundsVm, walletId);
        }
        else
        {
            UpdateSendButtonText();
            BuildUtxoList();
        }
    }

    private async Task LoadUtxosAsync(FundsViewModel fundsVm, string walletId)
    {
        try
        {
            await fundsVm.RefreshUtxoCacheAsync(walletId);
            var accountInfo = fundsVm.GetAccountBalanceInfo(walletId);
            if (accountInfo?.AccountInfo != null)
            {
                _utxos = accountInfo.AccountInfo.AllUtxos()
                    .Where(u => !u.PendingSpent)
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LoadUtxosAsync failed");
        }

        UpdateSendButtonText();
        BuildUtxoList();
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        switch (btn.Name)
        {
            case "CloseBtn":
                ShellVm?.HideModal();
                break;

            case "BtnHeaderSend":
                OpenSendModal();
                break;

            case "BtnHeaderReceive":
                OpenReceiveModal();
                break;
        }

        // Check for UTXO copy buttons (named "BtnCopyTxid_N")
        if (btn.Name?.StartsWith("BtnCopyTxid_") == true)
        {
            // Parse index from button name and copy the corresponding UTXO txid
            var indexStr = btn.Name["BtnCopyTxid_".Length..];
            if (int.TryParse(indexStr, out var idx) && idx >= 0 && idx < _utxos.Count)
            {
                var txid = _utxos[idx].outpoint?.transactionId ?? _utxos[idx].outpoint?.ToString() ?? "";
                ClipboardHelper.CopyToClipboard(this, txid);
            }
            e.Handled = true;
        }
    }

    /// <summary>
    /// Build the UTXO card list in code-behind.
    /// Vue: space-y-3 (12px gap). Each card: p-4 rounded-lg border, clickable.
    /// </summary>
    private void BuildUtxoList()
    {
        UtxoList.Children.Clear();

        if (_utxos.Count == 0)
        {
            EmptyUtxoState.IsVisible = true;
            return;
        }

        EmptyUtxoState.IsVisible = false;

        for (var i = 0; i < _utxos.Count; i++)
        {
            var utxo = _utxos[i];
            var card = CreateUtxoCard(utxo, i);
            UtxoList.Children.Add(card);
        }
    }

    /// <summary>
    /// Create a single UTXO card matching Vue design:
    /// p-4 rounded-lg border, clickable, shows txid + amount + confirmations + checkbox.
    /// Colors come from XAML styles via "UtxoCard" / "UtxoSelected" CSS classes (Rule #9).
    /// </summary>
    private Border CreateUtxoCard(UtxoData utxo, int index)
    {
        var outpointStr = utxo.outpoint?.ToString() ?? "";
        var txid = utxo.outpoint?.transactionId ?? outpointStr;

        var card = new Border
        {
            Name = $"UtxoCard_{index}",
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            Tag = outpointStr,
        };

        // Apply CSS class for DynamicResource styling (Rule #9: class toggle only)
        card.Classes.Add("UtxoCard");

        // Click to toggle selection
        card.PointerPressed += (_, _) =>
        {
            ToggleUtxoSelection(outpointStr, card, index);
        };

        // Card content: DockPanel with checkbox on right, info on left
        var dock = new DockPanel();

        // Right: checkbox 24x24
        var checkbox = CreateCheckbox(outpointStr, index);
        DockPanel.SetDock(checkbox, Dock.Right);
        dock.Children.Add(checkbox);

        // Left: info stack
        var infoStack = new StackPanel { Spacing = 8 };

        // Transaction ID row
        var txidSection = new StackPanel { Spacing = 2 };
        var txidLabel = new TextBlock
        {
            Text = "Transaction ID",
            FontSize = 12,
        };
        txidLabel.Classes.Add("TextMuted");
        txidSection.Children.Add(txidLabel);

        var txidRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        // Truncated green txid
        var truncated = txid.Length > 16
            ? txid[..16] + "..."
            : txid;

        txidRow.Children.Add(new TextBlock
        {
            Text = truncated,
            FontSize = 14,
            FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
            Foreground = this.FindResource("PillGreenText") as IBrush ?? new SolidColorBrush(Color.Parse("#4B7C5A")),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
        });

        // Copy button
        var copyBtn = new Button
        {
            Name = $"BtnCopyTxid_{index}",
            Padding = new Thickness(6),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
        };
        copyBtn.Classes.Add("ModalBtn");
        var iconControl = new Icon
        {
            Value = "fa-regular fa-copy",
            FontSize = 14,
        };
        iconControl.Classes.Add("TextMuted");
        copyBtn.Content = iconControl;
        txidRow.Children.Add(copyBtn);

        // Explorer button — open txid in block explorer
        var exploreBtn = new Button
        {
            Padding = new Thickness(6),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
        };
        exploreBtn.Classes.Add("ModalBtn");
        var exploreIcon = new Icon
        {
            Value = "fa-solid fa-arrow-up-right-from-square",
            FontSize = 14,
        };
        exploreIcon.Classes.Add("TextMuted");
        exploreBtn.Content = exploreIcon;
        var capturedTxid = txid; // capture for closure
        exploreBtn.Click += (_, _) =>
        {
            var networkService = App.Services.GetRequiredService<Angor.Shared.Services.INetworkService>();
            ExplorerHelper.OpenTransaction(networkService, capturedTxid);
        };
        txidRow.Children.Add(exploreBtn);

        txidSection.Children.Add(txidRow);
        infoStack.Children.Add(txidSection);

        // Amount + Confirmations row
        var statsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };

        // Amount (value is in satoshis, convert to BTC)
        double amountBtc = (double)utxo.value.ToUnitBtc();
        var amountPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var amountLabel = new TextBlock { Text = "Amount:", FontSize = 13 };
        amountLabel.Classes.Add("TextMuted");
        amountPanel.Children.Add(amountLabel);
        var amountValue = new TextBlock
        {
            Text = CurrencyService.FormatBtc(amountBtc),
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
        };
        amountValue.Classes.Add("TextStrong");
        amountPanel.Children.Add(amountValue);
        statsRow.Children.Add(amountPanel);

        // Confirmations (blockIndex > 0 means confirmed)
        var confPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var confLabel = new TextBlock { Text = "Status:", FontSize = 13 };
        confLabel.Classes.Add("TextMuted");
        confPanel.Children.Add(confLabel);
        var confValue = new TextBlock
        {
            Text = utxo.blockIndex > 0 ? "Confirmed" : "Unconfirmed",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
        };
        confValue.Classes.Add("TextStrong");
        confPanel.Children.Add(confValue);
        statsRow.Children.Add(confPanel);

        infoStack.Children.Add(statsRow);
        dock.Children.Add(infoStack);
        card.Child = dock;

        return card;
    }

    /// <summary>
    /// Create the 24x24 checkbox for UTXO selection.
    /// Vue: settings-toggle-button, 24x24, border-radius 4px
    /// Colors come from XAML styles via "UtxoCheckbox" / "UtxoChecked" CSS classes (Rule #9).
    /// </summary>
    private Border CreateCheckbox(string txid, int index)
    {
        var container = new Border
        {
            Name = $"Checkbox_{index}",
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = txid,
        };
        container.Classes.Add("UtxoCheckbox");
        return container;
    }

    private void ToggleUtxoSelection(string txid, Border card, int index)
    {
        var isNowSelected = !_selectedUtxos.Contains(txid);
        if (isNowSelected)
            _selectedUtxos.Add(txid);
        else
            _selectedUtxos.Remove(txid);

        // Rule #9: ONLY toggle CSS classes — zero color logic
        card.Classes.Set("UtxoSelected", isNowSelected);

        // Update checkbox via CSS class toggle
        var checkbox = card.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Name == $"Checkbox_{index}");
        if (checkbox != null)
        {
            checkbox.Classes.Set("UtxoChecked", isNowSelected);

            if (isNowSelected)
            {
                // Add checkmark — simple geometric shape, ok as Path per Rule #10
                var checkPath = new Avalonia.Controls.Shapes.Path
                {
                    Data = StreamGeometry.Parse("M5 13l4 4L19 7"),
                    Stroke = Brushes.White,
                    StrokeThickness = 2,
                    StrokeLineCap = PenLineCap.Round,
                    StrokeJoin = PenLineJoin.Round,
                    Width = 14,
                    Height = 14,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                checkbox.Child = checkPath;
            }
            else
            {
                checkbox.Child = null;
            }
        }

        UpdateSendButtonText();
    }

    /// <summary>
    /// Update Send button text based on selected UTXOs count.
    /// Vue: "Send" / "Send 1 UTXO" / "Send N UTXOs"
    /// </summary>
    private void UpdateSendButtonText()
    {
        var count = _selectedUtxos.Count;
        SendBtnText.Text = count switch
        {
            0 => "Send",
            1 => "Send 1 UTXO",
            _ => $"Send {count} UTXOs"
        };
    }

    private void OpenSendModal()
    {
        if (ShellVm is not { } shellVm) return;
        shellVm.HideModal();

        var sendModal = new SendFundsModal { DataContext = DataContext };
        sendModal.SetWallet(_walletName, _walletType, _walletBalance, _walletId);

        // Pre-fill amount if UTXOs selected
        if (_selectedUtxos.Count > 0)
        {
            var totalSats = _utxos
                .Where(u => _selectedUtxos.Contains(u.outpoint?.ToString() ?? ""))
                .Sum(u => u.value);
            sendModal.PrefillAmount((double)totalSats.ToUnitBtc());
        }

        shellVm.ShowModal(sendModal);
    }

    private void OpenReceiveModal()
    {
        if (ShellVm is not { } shellVm) return;
        shellVm.HideModal();

        var receiveModal = new ReceiveFundsModal { DataContext = DataContext };
        receiveModal.SetWallet(_walletName, _walletType, _walletId);
        shellVm.ShowModal(receiveModal);
    }

}

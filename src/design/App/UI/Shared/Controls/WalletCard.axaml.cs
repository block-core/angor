using Avalonia;
using Avalonia.Controls.Primitives;

namespace App.UI.Shared.Controls;

/// <summary>
/// A wallet card row showing wallet name, balance, type label, and action buttons.
/// Vue: Wallet list items in Funds section — icon, name/balance on left, 3 action buttons on right.
/// 
/// Buttons: Send (filled green gradient), Receive (outlined green), UTXO (outlined green).
/// Buttons are named "BtnSend", "BtnReceive", "BtnUtxo" for click handling in FundsView code-behind.
/// </summary>
public class WalletCard : TemplatedControl
{
    public static readonly StyledProperty<string?> WalletNameProperty =
        AvaloniaProperty.Register<WalletCard, string?>(nameof(WalletName));

    public static readonly StyledProperty<string?> BalanceProperty =
        AvaloniaProperty.Register<WalletCard, string?>(nameof(Balance));

    public static readonly StyledProperty<string?> WalletTypeProperty =
        AvaloniaProperty.Register<WalletCard, string?>(nameof(WalletType));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<WalletCard, string?>(nameof(Label));

    public static readonly StyledProperty<string?> WalletIdProperty =
        AvaloniaProperty.Register<WalletCard, string?>(nameof(WalletId));

    /// <summary>
    /// When true, shows the "Get Testnet Coins" faucet button (testnet only).
    /// </summary>
    public static readonly StyledProperty<bool> ShowFaucetProperty =
        AvaloniaProperty.Register<WalletCard, bool>(nameof(ShowFaucet));

    /// <summary>
    /// Pending (unconfirmed) balance string to display in yellow, or null/empty when none.
    /// </summary>
    public static readonly StyledProperty<string?> PendingBalanceProperty =
        AvaloniaProperty.Register<WalletCard, string?>(nameof(PendingBalance));

    /// <summary>
    /// Reserved balance string to display in blue, or null/empty when none.
    /// </summary>
    public static readonly StyledProperty<string?> ReservedBalanceProperty =
        AvaloniaProperty.Register<WalletCard, string?>(nameof(ReservedBalance));

    public string? WalletName
    {
        get => GetValue(WalletNameProperty);
        set => SetValue(WalletNameProperty, value);
    }

    public string? Balance
    {
        get => GetValue(BalanceProperty);
        set => SetValue(BalanceProperty, value);
    }

    public string? WalletType
    {
        get => GetValue(WalletTypeProperty);
        set => SetValue(WalletTypeProperty, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? WalletId
    {
        get => GetValue(WalletIdProperty);
        set => SetValue(WalletIdProperty, value);
    }

    public bool ShowFaucet
    {
        get => GetValue(ShowFaucetProperty);
        set => SetValue(ShowFaucetProperty, value);
    }

    public string? PendingBalance
    {
        get => GetValue(PendingBalanceProperty);
        set => SetValue(PendingBalanceProperty, value);
    }

    public string? ReservedBalance
    {
        get => GetValue(ReservedBalanceProperty);
        set => SetValue(ReservedBalanceProperty, value);
    }
}

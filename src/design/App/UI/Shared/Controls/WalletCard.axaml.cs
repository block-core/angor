using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using App.UI.Shared;
using ReactiveUI;

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

    /// <summary>
    /// When true, the refresh button shows a spinning icon.
    /// </summary>
    public static readonly StyledProperty<bool> IsRefreshingProperty =
        AvaloniaProperty.Register<WalletCard, bool>(nameof(IsRefreshing));

    /// <summary>
    /// When false, hides the bottom separator. Used for single-wallet groups and the last row.
    /// </summary>
    public static readonly StyledProperty<bool> ShowDividerProperty =
        AvaloniaProperty.Register<WalletCard, bool>(nameof(ShowDivider), true);

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

    public bool IsRefreshing
    {
        get => GetValue(IsRefreshingProperty);
        set => SetValue(IsRefreshingProperty, value);
    }

    public bool ShowDivider
    {
        get => GetValue(ShowDividerProperty);
        set => SetValue(ShowDividerProperty, value);
    }

    private IDisposable? _layoutSubscription;
    private Grid? _actionButtons;
    private Button? _btnSend;
    private Button? _btnReceive;
    private Button? _btnUtxo;
    private Button? _btnRefresh;
    private Button? _btnFaucet;
    private Border? _cardRoot;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _cardRoot = e.NameScope.Find<Border>("CardRoot");
        _actionButtons = e.NameScope.Find<Grid>("ActionButtons");
        _btnSend = e.NameScope.Find<Button>("BtnSend");
        _btnReceive = e.NameScope.Find<Button>("BtnReceive");
        _btnUtxo = e.NameScope.Find<Button>("BtnUtxo");
        _btnRefresh = e.NameScope.Find<Button>("BtnRefresh");
        _btnFaucet = e.NameScope.Find<Button>("BtnFaucet");
        _layoutSubscription?.Dispose();
        _layoutSubscription = LayoutModeService.Instance
            .WhenAnyValue(x => x.IsCompact)
            .Subscribe(ApplyResponsiveLayout);

        UpdateDivider();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ShowDividerProperty)
            UpdateDivider();

        if (change.Property == ShowFaucetProperty)
            ApplyResponsiveLayout(LayoutModeService.Instance.IsCompact);
    }

    private void UpdateDivider()
    {
        if (_cardRoot != null)
            _cardRoot.BorderThickness = ShowDivider ? new Thickness(0, 0, 0, 1) : new Thickness(0);
    }

    private void ApplyResponsiveLayout(bool isCompact)
    {
        if (_actionButtons == null) return;

        if (isCompact)
        {
            Grid.SetRow(_actionButtons, 1);
            Grid.SetColumn(_actionButtons, 0);
            Grid.SetColumnSpan(_actionButtons, 3);
            _actionButtons.HorizontalAlignment = HorizontalAlignment.Stretch;
            _actionButtons.Margin = new Thickness(0, 12, 0, 0);

            _actionButtons.RowDefinitions.Clear();
            _actionButtons.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _actionButtons.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _actionButtons.ColumnDefinitions.Clear();
            for (int i = 0; i < 6; i++)
                _actionButtons.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _actionButtons.ColumnSpacing = 8;
            _actionButtons.RowSpacing = 8;

            if (ShowFaucet)
            {
                PlaceButton(_btnUtxo, 0, 0, double.NaN, 52, 2);
                PlaceButton(_btnRefresh, 2, 0, double.NaN, 52, 2);
                PlaceButton(_btnFaucet, 4, 0, double.NaN, 52, 2);
            }
            else
            {
                PlaceButton(_btnUtxo, 0, 0, double.NaN, 52, 3);
                PlaceButton(_btnRefresh, 3, 0, double.NaN, 52, 3);
                PlaceButton(_btnFaucet, 4, 0, double.NaN, 52, 2);
            }

            PlaceButton(_btnSend, 0, 1, double.NaN, 52, 3);
            PlaceButton(_btnReceive, 3, 1, double.NaN, 52, 3);
        }
        else
        {
            Grid.SetRow(_actionButtons, 0);
            Grid.SetColumn(_actionButtons, 2);
            Grid.SetColumnSpan(_actionButtons, 1);
            _actionButtons.HorizontalAlignment = HorizontalAlignment.Right;
            _actionButtons.Margin = new Thickness(16, 0, 0, 0);

            _actionButtons.RowDefinitions.Clear();
            _actionButtons.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _actionButtons.ColumnDefinitions.Clear();
            _actionButtons.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(96)));
            _actionButtons.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(96)));
            _actionButtons.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(32)));
            _actionButtons.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(32)));
            _actionButtons.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(32)));
            _actionButtons.ColumnSpacing = 8;
            _actionButtons.RowSpacing = 0;

            PlaceButton(_btnSend, 0, 0, 96, 32);
            PlaceButton(_btnReceive, 1, 0, 96, 32);
            PlaceButton(_btnUtxo, 2, 0, 32, 32);
            PlaceButton(_btnRefresh, 3, 0, 32, 32);
            PlaceButton(_btnFaucet, 4, 0, 32, 32);
        }
    }

    private static void PlaceButton(Button? button, int column, int row, double width, double height, int columnSpan = 1)
    {
        if (button == null) return;

        Grid.SetColumn(button, column);
        Grid.SetRow(button, row);
        Grid.SetColumnSpan(button, columnSpan);
        button.Width = width;
        button.Height = height;
        button.MinHeight = height;
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _layoutSubscription?.Dispose();
        _layoutSubscription = null;
        base.OnDetachedFromVisualTree(e);
    }
}

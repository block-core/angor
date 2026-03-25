using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia2.UI.Shell;

namespace Avalonia2.UI.Sections.Funds;

/// <summary>
/// Send Funds Modal — thin code-behind that delegates to SendFundsModalViewModel.
/// Only retains button routing and fee CSS class toggling (visual-only concerns).
/// </summary>
public partial class SendFundsModal : UserControl, IBackdropCloseable
{
    private readonly SendFundsModalViewModel _vm;

    public SendFundsModal()
    {
        _vm = new SendFundsModalViewModel();
        DataContext = _vm;

        InitializeComponent();
        AddHandler(Button.ClickEvent, OnButtonClick);
    }

    public void OnBackdropCloseRequested() { }

    /// <summary>
    /// Set the source wallet info. Called by FundsView / WalletDetailModal before showing.
    /// </summary>
    public void SetWallet(string name, string type, string balance)
        => _vm.SetWallet(name, type, balance);

    /// <summary>
    /// Pre-fill the amount input (used when sending selected UTXOs).
    /// </summary>
    public void PrefillAmount(double amount)
        => _vm.PrefillAmount(amount);

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        switch (btn.Name)
        {
            case "CloseForm":
            case "BtnCancel":
                _vm.Close();
                break;

            case "BtnPct25":
                _vm.SetPercentage(0.25);
                break;
            case "BtnPct50":
                _vm.SetPercentage(0.50);
                break;
            case "BtnPct75":
                _vm.SetPercentage(0.75);
                break;
            case "BtnPct100":
                _vm.SetPercentage(1.0);
                break;

            case "BtnFeeLow":
            case "BtnFeeMedium":
            case "BtnFeeHigh":
                SelectFee(btn.Name);
                break;

            case "BtnSend":
                _vm.ValidateAndSend();
                break;

            case "BtnCopyTxid":
                _vm.CopyTxid(this);
                break;

            case "BtnDone":
                _vm.Close();
                break;
        }
    }

    private void SelectFee(string selectedName)
    {
        foreach (var btn in new[] { BtnFeeLow, BtnFeeMedium, BtnFeeHigh })
            btn.Classes.Set("FeeSelected", btn.Name == selectedName);
    }
}

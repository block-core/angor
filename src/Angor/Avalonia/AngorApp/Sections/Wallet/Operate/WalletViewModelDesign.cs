using System.Windows.Input;
using AngorApp.Model;

namespace AngorApp.Sections.Wallet.Operate;

public class WalletViewModelDesign : IWalletViewModel
{
    public IWallet Wallet { get; set; } = new WalletDesign();
    public ICommand Send { get; }
}
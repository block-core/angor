using System.Windows.Input;
using Angor.UI.Model;

namespace AngorApp.Sections.Wallet.Operate;

public class WalletViewModelDesign : IWalletViewModel
{
    public IWallet Wallet { get; set; } = new WalletDesign();
    public ICommand Send { get; }
}
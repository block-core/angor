using System.Windows.Input;
using AngorApp.Model;

namespace AngorApp.Sections.Wallet.Operate;

public interface IWalletViewModel
{
    public IWallet Wallet { get; }
    public ICommand Send { get; }
}
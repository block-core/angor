using System.Windows.Input;
using AngorApp.Sections.Wallet.Operate;

namespace AngorApp.Sections.Wallet;

public interface IWalletSectionViewModel
{
    ReactiveCommand<Unit, Maybe<Unit>> CreateWallet { get; }
    ReactiveCommand<Unit, Maybe<Unit>> RecoverWallet { get; }
    IObservable<bool> IsBusy { get; }
    public IWalletViewModel? Wallet { get; }
    public IObservable<bool> HasWallet { get; }
    public IObservable<bool> ShowCreateAndRecover { get; }
    public bool ShowCreate { get; }
}
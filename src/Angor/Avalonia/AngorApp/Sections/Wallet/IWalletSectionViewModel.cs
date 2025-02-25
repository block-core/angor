using System.Windows.Input;

namespace AngorApp.Sections.Wallet;

public interface IWalletSectionViewModel
{
    ReactiveCommand<Unit, Maybe<Unit>> CreateWallet { get; }
    ReactiveCommand<Unit, Maybe<Unit>> RecoverWallet { get; }
}
using AngorApp.Sections.Wallet.Operate;

namespace AngorApp.Sections.Wallet;

public interface IWalletSectionViewModel
{
    ReactiveCommand<Unit, Maybe<Unit>> Create { get; }
    ReactiveCommand<Unit, Maybe<Unit>> Import { get; }
    IObservable<bool> IsBusy { get; }
    public IWalletViewModel? ActiveWallet { get; }
    public IObservable<bool> HasWallet { get; }
    public bool CanCreateWallet { get; }
    ReactiveCommand<Unit, Result<Maybe<IWallet>>> LoadWallet { get; }
}
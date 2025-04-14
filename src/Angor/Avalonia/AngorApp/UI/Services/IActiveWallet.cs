namespace AngorApp.UI.Services;

public interface IActiveWallet
{
    Maybe<IWallet> Current { get; set; }
    IObservable<IWallet> CurrentChanged { get; }
    IObservable<bool> HasWallet { get; }
    void SetCurrent(IWallet wallet);
}
using Angor.UI.Model;
using CSharpFunctionalExtensions;

namespace AngorApp.Services;

public interface IActiveWallet
{
    Maybe<IWallet> Current { get; set; }
    IObservable<IWallet> CurrentChanged { get; }
    IObservable<bool> HasWallet { get; }
}
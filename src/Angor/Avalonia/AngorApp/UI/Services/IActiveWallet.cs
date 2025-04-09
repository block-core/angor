using System.Threading.Tasks;
using Angor.UI.Model;
using CSharpFunctionalExtensions;

namespace AngorApp.UI.Services;

public interface IActiveWallet
{
    Maybe<IWallet> Current { get; set; }
    IObservable<IWallet> CurrentChanged { get; }
    IObservable<bool> HasWallet { get; }
    Task<Result<Maybe<IWallet>>> TryGetCurrent();
}
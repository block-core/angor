using System.Threading.Tasks;

namespace AngorApp.UI.Services;

public interface IWalletRoot
{
    Task<Result<Maybe<IWallet>>> GetDefaultWalletAndActivate();
    IObservable<bool> HasDefault();
}
using System.Threading.Tasks;

namespace AngorApp.Sections.Wallet.CreateAndRecover;

public interface IWalletWizard
{
    Task<Maybe<Unit>> CreateNew();
    Task<Maybe<Unit>> Recover();
}